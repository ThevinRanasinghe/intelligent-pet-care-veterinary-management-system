# PetCare local performance benchmark - disposable local stack only.
# Env: PERF_API (default http://localhost:5145/api), E2E_PG_CONTAINER
# (default petcare-e2e-pg, used ONLY to activate the fixture org + seed slots).
# Windows PowerShell 5.1 compatible - concurrency via runspace pools.
$ErrorActionPreference = 'Stop'
$Api = $env:PERF_API; if (-not $Api) { $Api = 'http://localhost:5145/api' }
$PgContainer = $env:E2E_PG_CONTAINER; if (-not $PgContainer) { $PgContainer = 'petcare-e2e-pg' }
$ResultsDir = Join-Path $PSScriptRoot 'results'; New-Item -ItemType Directory -Force $ResultsDir | Out-Null

function Call($method, $path, $body, $token) {
  $h = @{}; if ($token) { $h.Authorization = "Bearer $token" }
  $p = @{ Uri = "$Api$path"; Method = $method; Headers = $h; ContentType = 'application/json' }
  if ($null -ne $body) { $p.Body = ($body | ConvertTo-Json -Depth 8) }
  try { $r = Invoke-WebRequest @p -UseBasicParsing; return @{ status = [int]$r.StatusCode; body = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) } }
  catch { $resp = $_.Exception.Response; $code = if ($resp) { [int]$resp.StatusCode } else { -1 }; return @{ status = $code; body = $null } }
}
function RunSql($q) { ($q | docker exec -i $PgContainer psql -U postgres -d petcare_e2e -t -A 2>&1 | Out-String).Trim() }
function Login($email, $pw) { (Call POST '/auth/login' @{ email = $email; password = $pw }).body }

# One HTTP call inside a runspace worker.
$Worker = {
  param($times, $fails, $uri, $method, $token, $body)
  $h = @{}; if ($token) { $h.Authorization = "Bearer $token" }
  $t = [Diagnostics.Stopwatch]::StartNew()
  try {
    $p = @{ Uri = $uri; Method = $method; Headers = $h; UseBasicParsing = $true; TimeoutSec = 300 }
    if ($null -ne $body) { $p.ContentType = 'application/json'; $p.Body = ($body | ConvertTo-Json -Depth 8) }
    Invoke-WebRequest @p | Out-Null
    $times.Enqueue($t.Elapsed.TotalMilliseconds)
  } catch {
    $code = -1; try { $code = [int]$_.Exception.Response.StatusCode } catch {}
    $fails.Enqueue("$code")
  } finally { $t.Stop() }
}

function Measure-Scenario($name, $n, $c, $uri, $method = 'GET', $token = $null, $body = $null) {
  $times = [System.Collections.Concurrent.ConcurrentQueue[double]]::new()
  $fails = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
  if ($c -le 1) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $n; $i++) { & $Worker $times $fails $uri $method $token $body }
    $sw.Stop()
  } else {
    $pool = [runspacefactory]::CreateRunspacePool(1, $c); $pool.Open()
    $handles = @()
    $sw = [Diagnostics.Stopwatch]::StartNew()
    for ($i = 0; $i -lt $n; $i++) {
      $ps = [powershell]::Create().AddScript($Worker)
      $ps.AddArgument($times).AddArgument($fails).AddArgument($uri).AddArgument($method).AddArgument($token).AddArgument($body) | Out-Null
      $ps.RunspacePool = $pool
      $handles += @{ ps = $ps; h = $ps.BeginInvoke() }
    }
    foreach ($x in $handles) { $x.ps.EndInvoke($x.h); $x.ps.Dispose() }
    $sw.Stop(); $pool.Close(); $pool.Dispose()
  }
  $arr = @($times.ToArray() | Sort-Object)
  $cnt = $arr.Count
  $p50 = if ($cnt) { $arr[[Math]::Floor(($cnt - 1) * 0.50)] } else { 0 }
  $p95 = if ($cnt) { $arr[[Math]::Floor(($cnt - 1) * 0.95)] } else { 0 }
  $failList = @($fails.ToArray())
  $result = [ordered]@{
    scenario = $name; n = $n; concurrency = $c
    success = $cnt; failures = $failList.Count; failureCodes = ($failList | Group-Object | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ','
    avgMs = [Math]::Round(($arr | Measure-Object -Average).Average, 1)
    p50Ms = [Math]::Round($p50, 1); p95Ms = [Math]::Round($p95, 1)
    maxMs = [Math]::Round(($arr | Measure-Object -Maximum).Maximum, 1)
    wallMs = [Math]::Round($sw.Elapsed.TotalMilliseconds, 0)
    throughputRps = [Math]::Round($cnt / [Math]::Max(0.001, $sw.Elapsed.TotalSeconds), 1)
  }
  Write-Host ("{0,-34} n={1,-3} ok={2,-3} fail={3,-3} avg={4,8:N1}ms p50={5,8:N1} p95={6,8:N1} max={7,9:N1} thr={8,7:N1}/s" -f $name, $n, $result.success, $result.failures, $result.avgMs, $result.p50Ms, $result.p95Ms, $result.maxMs, $result.throughputRps)
  return [pscustomobject]$result
}

# ---------------- fixture ----------------
$tag = (Get-Date -Format 'HHmmss')
$Pw = 'PerfPass!2026'
$date = (Get-Date).AddDays(1); while ($date.DayOfWeek -in 'Saturday','Sunday') { $date = $date.AddDays(1) }
$dateStr = $date.ToString('yyyy-MM-dd')

Call POST '/auth/register/pet-owner' @{ firstName='Perf'; lastName='Owner'; email="powner$tag@e2e.test"; password=$Pw; confirmPassword=$Pw } | Out-Null
$owner = Login "powner$tag@e2e.test" $Pw
Call POST '/auth/register/organization' @{ organizationName="Perf Clinic $tag"; organizationEmail="pclinic$tag@e2e.test"; organizationPhone='011'; address='x'; city='Colombo'; country='Sri Lanka'; managerFirstName='P'; managerLastName='M'; managerEmail="pcm$tag@e2e.test"; password=$Pw; confirmPassword=$Pw; latitude=6.848; longitude=79.9265 } | Out-Null
RunSql "UPDATE ""Organizations"" SET ""Status""=1, ""IsActive""=true WHERE ""Email""='pclinic$tag@e2e.test'" | Out-Null
$cm = Login "pcm$tag@e2e.test" $Pw
$r = Call POST '/manager/users/veterinarians' @{ firstName='Perf'; lastName='Vet'; email="pvet$tag@e2e.test" } $cm.token
$vet = Login "pvet$tag@e2e.test" $r.body.temporaryPassword
$vetId = RunSql "SELECT ""Id"" FROM ""Veterinarians"" WHERE ""UserId""='$($vet.userId)'"
RunSql @"
INSERT INTO "AppointmentSlots" ("Id","VeterinarianId","Date","StartTime","EndTime","Branch","Status","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(), '$vetId', DATE '$dateStr', make_time(h,0,0), make_time(h+1,0,0), 'Main', 'Available', now(), now()
FROM generate_series(9,17) AS h
"@ | Out-Null
$orgId = RunSql "SELECT ""Id"" FROM ""Organizations"" WHERE ""Email""='pclinic$tag@e2e.test'"
$ownerProfileId = RunSql "SELECT ""Id"" FROM ""PetOwners"" WHERE ""UserId""='$($owner.userId)'"
$r = Call POST '/pets' @{ ownerId=$ownerProfileId; name='PerfPet'; species='Dog'; breed='Lab'; gender='Male'; weight=10 } $owner.token
$petId = $r.body.id

# ---------------- scenarios ----------------
$results = @()
$results += Measure-Scenario 'swagger.json (unauth baseline)' 50 10 "$Api/../swagger/v1/swagger.json"
$results += Measure-Scenario 'GET /consultations (manager)' 50 10 "$Api/consultations" 'GET' $cm.token
$results += Measure-Scenario 'GET /appointments/available-slots' 50 10 "$Api/appointments/available-slots?date=$dateStr" 'GET' $cm.token
$results += Measure-Scenario 'POST /consultations create (owner)' 20 5 "$Api/consultations" 'POST' $owner.token (@{ petId=$petId; ownerId=$ownerProfileId; symptoms='perf write'; urgency='Low'; preferredDate="$dateStr`T13:00:00"; preferredTime='13:00:00'; budget=5000; organizationId=$orgId })

# Agentic scenarios - fresh consultations, sequential (Gemini quota).
$timesRun = [System.Collections.Concurrent.ConcurrentQueue[double]]::new()
$timesApprove = [System.Collections.Concurrent.ConcurrentQueue[double]]::new()
$failsRun = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
$failsApprove = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
$analysisTimes = [System.Collections.Concurrent.ConcurrentQueue[double]]::new()
$analysisFails = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()

$wfs = @()
foreach ($t in '09:00:00','11:00:00','15:00:00') {
  $c = Call POST '/consultations' @{ petId=$petId; ownerId=$ownerProfileId; symptoms="perf agentic $t"; urgency='Medium'; preferredDate="$dateStr`T$t"; preferredTime=$t; budget=10000; organizationId=$orgId } $owner.token
  if ($c.status -eq 201) {
    Call POST "/consultations/$($c.body.id)/submit" $null $owner.token | Out-Null
    $w = Call GET "/agent-workflows/by-consultation/$($c.body.id)" $null $cm.token
    if ($w.status -eq 200) { $wfs += @{ id = $w.body.id; cons = $c.body.id } }
  }
}
foreach ($w in $wfs) { & $Worker $timesRun $failsRun "$Api/agent-workflows/$($w.id)/run" 'POST' $cm.token $null }
foreach ($w in $wfs) { & $Worker $timesApprove $failsApprove "$Api/agent-workflows/$($w.id)/approve" 'POST' $cm.token (@{ comments = 'perf' }) }
foreach ($w in $wfs) { & $Worker $analysisTimes $analysisFails "$Api/consultations/$($w.cons)/analysis" 'GET' $cm.token $null }

function Aggregate($name, $times, $fails, $n, $c, $wallMs) {
  $arr = @($times.ToArray() | Sort-Object)
  $cnt = $arr.Count
  $failList = @($fails.ToArray())
  [pscustomobject][ordered]@{
    scenario = $name; n = $n; concurrency = $c
    success = $cnt; failures = $failList.Count; failureCodes = ($failList | Group-Object | ForEach-Object { "$($_.Name)x$($_.Count)" }) -join ','
    avgMs = [Math]::Round(($arr | Measure-Object -Average).Average, 1)
    p50Ms = [Math]::Round(($arr[[Math]::Floor(($cnt-1)*0.5)]), 1)
    p95Ms = [Math]::Round(($arr[[Math]::Floor(($cnt-1)*0.95)]), 1)
    maxMs = [Math]::Round(($arr | Measure-Object -Maximum).Maximum, 1)
    wallMs = [Math]::Round($wallMs, 0)
    throughputRps = [Math]::Round($cnt / [Math]::Max(0.001, $wallMs/1000), 1)
  }
}

# wall time for sequential agentic loops isn't tracked per-scenario; use sum.
$results += Aggregate 'agentic workflow run' $timesRun $failsRun $wfs.Count 1 ($timesRun.ToArray() | Measure-Object -Sum).Sum
$results += Aggregate 'agentic approve (resume+booking)' $timesApprove $failsApprove $wfs.Count 1 ($timesApprove.ToArray() | Measure-Object -Sum).Sum
$results += Aggregate 'GET consultation analysis (agent)' $analysisTimes $analysisFails $wfs.Count 1 ($analysisTimes.ToArray() | Measure-Object -Sum).Sum

foreach ($s in @($results | Select-Object -Last 3)) {
  Write-Host ("{0,-34} n={1,-3} ok={2,-3} fail={3,-3} avg={4,8:N1}ms p50={5,8:N1} p95={6,8:N1} max={7,9:N1}ms" -f $s.scenario, $s.n, $s.success, $s.failures, $s.avgMs, $s.p50Ms, $s.p95Ms, $s.maxMs)
}

$out = [ordered]@{
  generatedAt = (Get-Date).ToUniversalTime().ToString('o')
  api = $Api
  environment = [ordered]@{
    ps = $PSVersionTable.PSVersion.ToString()
    dotnet = (dotnet --version)
    python = (& "..\..\agentic-service\.venv\Scripts\python.exe" --version 2>&1)
    postgres = 'postgres:16 (docker container petcare-e2e-pg, localhost:55432)'
    geminiModel = ((Get-Content "..\..\agentic-service\.env" | Where-Object { $_ -match '^GEMINI_MODEL=' }) -replace '^GEMINI_MODEL=','')
    os = [System.Environment]::OSVersion.VersionString
    cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
    ramGB = [Math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1GB, 1)
  }
  results = $results
}
$out | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ResultsDir 'latest.json') -Encoding UTF8
Write-Host "`nResults written to $(Join-Path $ResultsDir 'latest.json')"
