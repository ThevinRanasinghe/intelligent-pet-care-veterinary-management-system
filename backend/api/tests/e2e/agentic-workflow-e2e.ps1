# PetCare agentic-workflow E2E - runs against a LOCAL disposable stack only.
#   API:      $env:E2E_API            (default http://localhost:5145/api)
#   Postgres: $env:E2E_PG_CONTAINER   (default petcare-e2e-pg) via `docker exec ... psql`
#             used ONLY to activate the freshly registered organization.
#   Agentic:  real Python service on :8000 (the script NEVER calls it directly -
#             every workflow interaction goes through the ASP.NET API).
# Never points at Supabase. Exits non-zero on the first failed assertion.
$ErrorActionPreference = 'Stop'
$Api = $env:E2E_API; if (-not $Api) { $Api = 'http://localhost:5145/api' }
$PgContainer = $env:E2E_PG_CONTAINER; if (-not $PgContainer) { $PgContainer = 'petcare-e2e-pg' }
$pass = 0; $fail = 0

function Step($name) { Write-Host "`n### $name" -ForegroundColor Cyan }
function Assert($cond, $msg) { if ($cond) { $script:pass++; Write-Host "  PASS  $msg" -ForegroundColor Green } else { $script:fail++; Write-Host "  FAIL  $msg" -ForegroundColor Red; throw "Assertion failed: $msg" } }
function Call($method, $path, $body, $token) {
  $h = @{}; if ($token) { $h.Authorization = "Bearer $token" }
  $p = @{ Uri = "$Api$path"; Method = $method; Headers = $h; ContentType = 'application/json' }
  if ($null -ne $body) { $p.Body = ($body | ConvertTo-Json -Depth 8) }
  try { $r = Invoke-WebRequest @p -UseBasicParsing; return @{ status = [int]$r.StatusCode; body = ($(if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null })) } }
  catch { $resp = $_.Exception.Response; $code = if ($resp) { [int]$resp.StatusCode } else { -1 }; $txt = ''; try { $sr = New-Object IO.StreamReader($resp.GetResponseStream()); $txt = $sr.ReadToEnd() } catch {}; return @{ status = $code; body = $(try { $txt | ConvertFrom-Json } catch { $txt }) } }
}
function RunSql($q) { ($q | docker exec -i $PgContainer psql -U postgres -d petcare_e2e -t -A 2>&1 | Out-String).Trim() }
function Login($email, $pw) { $r = Call POST '/auth/login' @{ email = $email; password = $pw }; Assert ($r.status -eq 200) "login $email"; return $r.body }
# Gemini free-tier allows ~15 generate calls/minute; each workflow run
# fires several LLM calls back-to-back. Pause before LLM-triggering
# endpoints so the suite stays under quota.
function QuotaPause([int]$seconds = 20) { Start-Sleep -Seconds $seconds }
# Next business day within the 09:00-18:00 one-hour-slot operating window -
# all three consultations share it so seeded availability covers every run.
$WorkDate = (Get-Date).AddDays(1)
while ($WorkDate.DayOfWeek -in 'Saturday','Sunday') { $WorkDate = $WorkDate.AddDays(1) }

function New-Consultation($petId, $ownerProfileId, $orgId, $token, $notes, $time = '09:00:00') {
  # With a single vet, two requests for the same hour conflict (409) -
  # each workflow run gets its own preferred hour on the same work date.
  $r = Call POST '/consultations' @{ petId = $petId; ownerId = $ownerProfileId; symptoms = $notes; urgency = 'Medium'; preferredDate = $WorkDate.ToString('yyyy-MM-ddT') + $time; preferredTime = $time; budget = 10000; additionalNotes = 'e2e workflow run'; organizationId = $orgId } $token
  Assert ($r.status -eq 201) "consultation created ($notes)"
  $id = $r.body.id
  $r = Call POST "/consultations/$id/submit" $null $token
  Assert ($r.status -eq 200 -and $r.body.status -eq 'Submitted') "consultation submitted ($notes)"
  return $id
}

$tag = (Get-Date -Format 'HHmmss')
$Pw = 'E2ePass!2026'

Step 'SETUP - owner, org (activated via psql in container), manager, vet, pet'
$r = Call POST '/auth/register/pet-owner' @{ firstName = 'Nimal'; lastName = 'Perera'; email = "owner$tag@e2e.test"; password = $Pw; confirmPassword = $Pw }
Assert ($r.status -eq 201) 'owner registered'
$owner = Login "owner$tag@e2e.test" $Pw

$r = Call POST '/auth/register/organization' @{ organizationName = "E2E WF Clinic $tag"; organizationEmail = "clinic$tag@e2e.test"; organizationPhone = '0112223334'; address = '1 Vet Rd'; city = 'Colombo'; country = 'Sri Lanka'; managerFirstName = 'Mala'; managerLastName = 'Manager'; managerEmail = "cm$tag@e2e.test"; password = $Pw; confirmPassword = $Pw; latitude = 6.8480; longitude = 79.9265 }
Assert ($r.status -eq 201) 'organization registered (Pending)'
RunSql "UPDATE ""Organizations"" SET ""Status""=1, ""IsActive""=true WHERE ""Email""='clinic$tag@e2e.test'" | Out-Null
$cm = Login "cm$tag@e2e.test" $Pw
Assert ($cm.role -eq 'ClinicManager') 'manager role'
$OrgId = RunSql "SELECT ""Id"" FROM ""Organizations"" WHERE ""Email""='clinic$tag@e2e.test'"
Assert ($OrgId) "org id resolved"

$r = Call POST '/manager/users/veterinarians' @{ firstName = 'Kasun'; lastName = 'Vet'; email = "vet$tag@e2e.test" } $cm.token
Assert ($r.status -eq 201) 'vet account created'
$vet = Login "vet$tag@e2e.test" $r.body.temporaryPassword

# The booking model materializes a Reserved slot only when an appointment is
# assigned; /appointments/available-slots therefore only sees pre-posted
# 'Available' slot rows. Post hourly availability for the vet on the work
# date - the fixture a real clinic's availability feed would provide.
$vetId = RunSql "SELECT ""Id"" FROM ""Veterinarians"" WHERE ""UserId""='$($vet.userId)'"
Assert ($vetId) "vet id resolved ($vetId)"
RunSql @"
INSERT INTO "AppointmentSlots" ("Id","VeterinarianId","Date","StartTime","EndTime","Branch","Status","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(), '$vetId', DATE '$($WorkDate.ToString('yyyy-MM-dd'))',
       make_time(h,0,0), make_time(h+1,0,0), 'Main', 'Available', now(), now()
FROM generate_series(9,17) AS h
"@ | Out-Null
$slotCount = RunSql "SELECT count(*) FROM ""AppointmentSlots"" WHERE ""VeterinarianId""='$vetId' AND ""Status""='Available'"
Assert ($slotCount -eq '9') "seeded 9 available slots for the vet ($slotCount)"

$ownerProfileId = RunSql "SELECT ""Id"" FROM ""PetOwners"" WHERE ""UserId"" = '$($owner.userId)'"
$r = Call POST '/pets' @{ ownerId = $ownerProfileId; name = 'Max'; species = 'Dog'; breed = 'Labrador'; gender = 'Male'; weight = 20 } $owner.token
Assert ($r.status -eq 201) 'pet registered'; $petId = $r.body.id

Step 'WORKFLOW 1 - submit auto-creates workflow (status Created)'
$consId = New-Consultation $petId $ownerProfileId $OrgId $owner.token 'Vomiting since yesterday'
$r = Call GET "/agent-workflows/by-consultation/$consId" $null $cm.token
Assert ($r.status -eq 200 -and $r.body.status -eq 'Created') 'manager sees auto-created workflow (Created)'
$wfId = $r.body.id
$r = Call GET "/agent-workflows/by-consultation/$consId" $null $owner.token
Assert ($r.status -eq 200 -and $r.body.status -eq 'Created' -and $r.body.workflowId -eq $wfId -and $null -eq $r.body.steps) 'owner sees reduced status DTO only'

Step 'AUTHORIZATION - PetOwner cannot run; Veterinarian cannot approve'
$r = Call POST "/agent-workflows/$wfId/run" $null $owner.token
Assert ($r.status -eq 403) 'PetOwner POST run -> 403'

Step 'RUN - manager runs supervisor graph (calls real agentic service via backend)'
QuotaPause
$r = Call POST "/agent-workflows/$wfId/run" $null $cm.token
if ($r.status -ne 200) { Write-Host ("run status=$($r.status) body=$($r.body | ConvertTo-Json -Depth 6 -Compress)") -ForegroundColor Yellow }
Assert ($r.status -eq 200) 'manager POST run -> 200'
$wf = $r.body
if ($wf.status -eq 'Failed') { Write-Host ("WORKFLOW FAILED: $($wf.failureReason)") -ForegroundColor Red; throw "Agentic run failed: $($wf.failureReason)" }
Assert ($wf.status -eq 'PendingManagerApproval') "status PendingManagerApproval ($($wf.status))"
Assert (@($wf.plan.steps).Count -ge 4) "plan has >=4 steps ($(@($wf.plan.steps).Count))"
Assert (@(@($wf.plan.steps) | Where-Object { $_.agent -eq 'consultation_agent' }).Count -ge 1) 'plan includes consultation_agent'
Assert (@(@($wf.plan.steps) | Where-Object { $_.agent -eq 'scheduling_agent' }).Count -ge 1) 'plan includes scheduling_agent'
Assert ($null -ne $wf.proposal) 'proposal present'
Assert ($null -eq $wf.approvedAction) 'approvedAction null before approval'
Assert (@(@($wf.approvals) | Where-Object { $_.status -eq 'Pending' }).Count -eq 1) 'one pending approval recorded'

$r = Call POST "/agent-workflows/$wfId/approve" @{ comments = 'vet attempt' } $vet.token
Assert ($r.status -eq 403) 'Veterinarian POST approve -> 403'
$r = Call POST "/agent-workflows/$wfId/reject" @{ } $cm.token
Assert ($r.status -eq 400) 'reject WITHOUT comments -> 400'

Step 'APPROVE - booking executed by backend after manager approval'
$r = Call POST "/agent-workflows/$wfId/approve" @{ comments = 'looks good' } $cm.token
Assert ($r.status -eq 200) 'manager POST approve -> 200'
$wf = $r.body
if ($wf.status -eq 'Failed') { Write-Host ("APPROVE FAILED: $($wf.failureReason)") -ForegroundColor Red; throw "Approve failed: $($wf.failureReason)" }
Assert ($wf.status -eq 'AwaitingExamination') "status AwaitingExamination ($($wf.status))"
Assert ($null -ne $wf.approvedAction) 'approvedAction persisted'
$r = Call GET "/consultations/$consId" $null $cm.token
Assert ($r.body.status -eq 'AppointmentConfirmed') 'consultation -> AppointmentConfirmed (booking executed)'
$r = Call GET "/consultations/$consId" $null $owner.token
Assert ($r.body.status -eq 'AppointmentConfirmed' -and $r.body.agentWorkflowStatus -eq 'AwaitingExamination') 'owner sees AppointmentConfirmed + workflow status'

$r = Call POST "/agent-workflows/$wfId/approve" @{ comments = 'again' } $cm.token
Assert ($r.status -eq 409) 'duplicate approve -> 409'

Step 'HISTORY - plan, steps, approvals, trajectory events'
$r = Call GET "/agent-workflows/$wfId/history" $null $cm.token
Assert ($r.status -eq 200) 'history 200'
$h = $r.body
Assert ($null -ne $h.workflow.plan) 'history carries plan'
Assert (@($h.steps).Count -ge 3) "history >=3 steps ($(@($h.steps).Count))"
$agents = @($h.steps | ForEach-Object { $_.agentName })
$tasks = @($h.steps | ForEach-Object { $_.task })
Assert (($agents -contains 'consultation_agent') -and ($agents -contains 'scheduling_agent') -and ($tasks -contains 'backend:book_appointment')) "steps cover consultation_agent/scheduling_agent/book_appointment (agents=$($agents -join ','), tasks=$($tasks -join ','))"
$appr = @($h.approvals | Where-Object { $_.status -eq 'Approved' })
Assert ($appr.Count -eq 1 -and $null -ne $appr[0].decidedByUserId -and $null -ne $appr[0].decidedAt) 'one Approved approval with decidedBy/decidedAt'
$events = @($h.events)
Assert ($events.Count -gt 0) 'trajectory events recorded'
$etypes = @($events | ForEach-Object { $_.eventType })
foreach ($e in 'delegated','step_validated','route_human_approval','decision_approved','final_validation_passed','backend_action_emitted') {
  Assert (($etypes -contains $e)) "trajectory includes $e"
}
Assert (($etypes -contains 'plan_created') -or ($etypes -contains 'plan_fallback')) 'trajectory includes plan_created|plan_fallback'
$seqs = @($events | ForEach-Object { $_.seq })
$strictlyIncreasing = $true; for ($i = 1; $i -lt $seqs.Count; $i++) { if ($seqs[$i] -le $seqs[$i-1]) { $strictlyIncreasing = $false } }
Assert ($strictlyIncreasing) 'trajectory seq strictly increasing'

Step 'CONTINUATION - real business events advance the SAME workflow id'
# start is idempotent - one workflow per consultation for its whole lifecycle
$r = Call POST '/agent-workflows/start' @{ consultationRequestId = $consId } $cm.token
Assert ($r.status -eq 200 -and $r.body.id -eq $wfId) 'start returns SAME workflow id (no duplicate)'

# The agents the supervisor actually planned - later assertions follow ITS
# plan, proving routing is plan-driven (a short valid plan would skip
# clinical steps rather than forcing all four agents).
$planAgents = @(@($wf.plan.steps) | Where-Object { $_.type -eq 'agent' } | ForEach-Object { $_.agent })
Write-Host "  INFO  supervisor plan delegates: $($planAgents -join ', ')" -ForegroundColor DarkCyan

$apptId = RunSql "SELECT ""Id"" FROM ""Appointments"" WHERE ""ConsultationRequestId""='$consId'"
Assert ($apptId) "approved booking produced a real appointment ($apptId)"

# Vet records the examination against the booked appointment.
$r = Call POST '/examinations' @{ petId = $petId; veterinarianId = [Guid]$vetId; appointmentId = [Guid]$apptId; veterinarianCharge = 3500; symptoms = 'Vomiting, lethargy'; notes = 'e2e exam'; examinationDate = (Get-Date).ToUniversalTime().ToString('o') } $vet.token
Assert ($r.status -eq 201) "examination recorded by vet ($($r.status))"
$examId = $r.body.id

# The vet's AI-assist call routes INTO the waiting workflow: the
# examination_recorded event resumes the same graph and runs diagnosis_agent.
if ($planAgents -contains 'diagnosis_agent') {
  QuotaPause
  $r = Call GET "/examinations/$examId/recommendations" $null $vet.token
  Assert ($r.status -eq 200) 'vet recommendations call -> 200 (routes through workflow)'
  $r = Call GET "/agent-workflows/$wfId" $null $cm.token
  if ($planAgents -contains 'inventory_agent') {
    Assert ($r.body.status -eq 'AwaitingPrescription') "workflow -> AwaitingPrescription ($($r.body.status))"
  } else {
    Assert ($r.body.status -eq 'Completed') "short plan -> Completed after diagnosis ($($r.body.status))"
  }
  $diagStep = @($r.body.steps | Where-Object { $_.agentName -eq 'diagnosis_agent' })
  Assert ($diagStep.Count -ge 1 -and $diagStep[0].status -eq 'Completed') 'diagnosis_agent step recorded Completed'
} else {
  Write-Host "  INFO  plan has no diagnosis step - workflow completes without it" -ForegroundColor DarkCyan
  $r = Call POST "/agent-workflows/$wfId/events" @{ eventType = 'examination_recorded'; referenceId = $examId } $cm.token
  Assert ($r.status -eq 200 -and $r.body.status -eq 'Completed') 'examination event completes short plan'
}

# Clinical chain continues: diagnosis -> treatment record -> medicine request.
$r = Call POST '/diagnoses' @{ examinationId = [Guid]$examId; conditionName = 'Gastritis'; description = 'e2e diagnosis'; severity = 'Moderate' } $vet.token
Assert ($r.status -eq 201) "diagnosis saved by vet ($($r.status))"
$diagId = $r.body.id
$r = Call POST '/treatmentrecords' @{ diagnosisId = [Guid]$diagId; procedureName = 'Medication'; notes = 'e2e treatment' } $vet.token
Assert ($r.status -eq 201) "treatment record saved ($($r.status))"
$trId = $r.body.id

# Inventory officer + stocked medicine for the prescription.
$r = Call POST '/manager/users/inventory-officers' @{ firstName = 'Iresha'; lastName = 'Officer'; email = "io$tag@e2e.test" } $cm.token
Assert ($r.status -eq 201) 'inventory officer account created'
$io = Login "io$tag@e2e.test" $r.body.temporaryPassword
$r = Call POST '/medicines' @{ name = 'Amoxicillin E2E'; category = 'Antibiotic'; description = 'e2e'; dosageForm = 'Capsule'; strength = '250mg'; unitPrice = 50; manufacturer = 'E2E'; reorderLevel = 5 } $io.token
Assert ($r.status -eq 201) "medicine registered ($($r.status))"
$medId = $r.body.id
RunSql "UPDATE ""Medicines"" SET ""TotalQuantity""=100 WHERE ""Id""='$medId'" | Out-Null

$r = Call POST '/prescriptions' @{ treatmentRecordId = [Guid]$trId; items = @(@{ medicineId = [Guid]$medId; dosage = '250mg'; durationDays = 5; quantity = 10; frequency = 'twice daily'; route = 'Oral'; instructions = 'with food' }) } $vet.token
Assert ($r.status -eq 201) "medicine request created ($($r.status))"

# The IO's inventory-plan call routes INTO the waiting workflow:
# prescription_created resumes the same graph and runs inventory_agent.
if ($planAgents -contains 'inventory_agent') {
  QuotaPause
  $r = Call GET "/prescriptions/treatment/$trId/inventory-plan" $null $io.token
  Assert ($r.status -eq 200) 'inventory-plan call -> 200 (routes through workflow)'
  $r = Call GET "/agent-workflows/$wfId" $null $cm.token
  Assert ($r.body.status -eq 'Completed') "workflow -> Completed ($($r.body.status))"
  $invStep = @($r.body.steps | Where-Object { $_.agentName -eq 'inventory_agent' })
  Assert ($invStep.Count -ge 1 -and $invStep[0].status -eq 'Completed') 'inventory_agent step recorded Completed'
} else {
  Write-Host "  INFO  plan has no inventory step - asserting Completed without it" -ForegroundColor DarkCyan
  $r = Call GET "/agent-workflows/$wfId" $null $cm.token
  Assert ($r.body.status -eq 'Completed') "workflow -> Completed ($($r.body.status))"
}

Step 'WORKFLOW ID - identical across the full business lifecycle'
$r = Call GET "/agent-workflows/by-consultation/$consId" $null $cm.token
Assert ($r.status -eq 200 -and $r.body.id -eq $wfId) 'by-consultation resolves the SAME workflow id'
$r = Call GET "/agent-workflows/$wfId/history" $null $cm.token
$h = $r.body
Assert ($h.workflow.id -eq $wfId) 'history belongs to the same workflow id'
$stepAgents = @($h.steps | ForEach-Object { $_.agentName } | Where-Object { $_ })
foreach ($a in $planAgents) { Assert (($stepAgents -contains $a)) "planned specialist executed: $a" }
foreach ($a in $stepAgents) { Assert (($planAgents -contains $a)) "no specialist ran outside the plan: $a" }
$delegatedEvents = @($h.events | Where-Object { $_.eventType -eq 'delegated' })
Assert ($delegatedEvents.Count -eq $planAgents.Count) "delegation count matches plan ($($delegatedEvents.Count)/$($planAgents.Count))"
$etypes = @($h.events | ForEach-Object { $_.eventType })
Assert (($etypes -contains 'awaiting_event')) 'trajectory shows the prerequisite wait (awaiting_event)'
Assert (($etypes -contains 'examination_recorded')) 'backend marker event examination_recorded persisted'
if ($planAgents -contains 'inventory_agent') {
  Assert (($etypes -contains 'prescription_created')) 'backend marker event prescription_created persisted'
}
$seqs = @($h.events | ForEach-Object { $_.seq })
$strictlyIncreasing = $true; for ($i = 1; $i -lt $seqs.Count; $i++) { if ($seqs[$i] -le $seqs[$i-1]) { $strictlyIncreasing = $false } }
Assert ($strictlyIncreasing) 'trajectory seq strictly increasing across whole lifecycle'

Step 'TENANT - foreign organization cannot read or resume this workflow'
$r = Call POST '/auth/register/organization' @{ organizationName = "Foreign Org $tag"; organizationEmail = "foreign$tag@e2e.test"; organizationPhone = '0113334445'; address = 'x'; city = 'Colombo'; country = 'Sri Lanka'; managerFirstName = 'F'; managerLastName = 'M'; managerEmail = "fcm$tag@e2e.test"; password = $Pw; confirmPassword = $Pw; latitude = 6.9; longitude = 79.9 }
Assert ($r.status -eq 201) 'foreign org registered'
RunSql "UPDATE ""Organizations"" SET ""Status""=1, ""IsActive""=true WHERE ""Email""='foreign$tag@e2e.test'" | Out-Null
$cm2 = Login "fcm$tag@e2e.test" $Pw
$r = Call GET "/agent-workflows/$wfId" $null $cm2.token
Assert ($r.status -eq 404) 'foreign-org manager GET workflow -> 404'
$r = Call POST "/agent-workflows/$wfId/run" $null $cm2.token
Assert ($r.status -eq 404) 'foreign-org manager POST run -> 404 (cannot resume foreign workflow)'

Step 'WORKFLOW 2 - run then reject: no booking, consultation stays Submitted'
$cons2 = New-Consultation $petId $ownerProfileId $OrgId $owner.token 'Limping front leg' '11:00:00'
$r = Call GET "/agent-workflows/by-consultation/$cons2" $null $cm.token
$wf2 = $r.body.id
QuotaPause
$r = Call POST "/agent-workflows/$wf2/run" $null $cm.token
Assert ($r.status -eq 200) 'wf2 run -> 200'
if ($r.body.status -eq 'Failed') { Write-Host ("wf2 FAILED at run: $($r.body.failureReason)") -ForegroundColor Red; throw "wf2 run failed: $($r.body.failureReason)" }
Assert ($r.body.status -eq 'PendingManagerApproval') 'wf2 PendingManagerApproval'
$r = Call POST "/agent-workflows/$wf2/reject" @{ comments = 'not needed, owner cancelled' } $cm.token
Assert ($r.status -eq 200 -and $r.body.status -eq 'Rejected') 'wf2 rejected -> Rejected'
$r = Call GET "/consultations/$cons2" $null $cm.token
Assert ($r.body.status -eq 'Submitted') 'rejected consultation stays Submitted (no appointment)'

Step 'WORKFLOW 3 - revision request re-pends approval'
$cons3 = New-Consultation $petId $ownerProfileId $OrgId $owner.token 'Itchy skin' '14:00:00'
$r = Call GET "/agent-workflows/by-consultation/$cons3" $null $cm.token
$wf3 = $r.body.id
QuotaPause
$r = Call POST "/agent-workflows/$wf3/run" $null $cm.token
Assert ($r.status -eq 200) 'wf3 run -> 200'
if ($r.body.status -eq 'Failed') { Write-Host ("wf3 FAILED at run: $($r.body.failureReason)") -ForegroundColor Red; throw "wf3 run failed: $($r.body.failureReason)" }
QuotaPause
$r = Call POST "/agent-workflows/$wf3/revision" @{ comments = 'prefer a later slot' } $cm.token
Assert ($r.status -eq 200) 'wf3 revision -> 200'
if ($r.body.status -eq 'PendingManagerApproval') {
  Assert (@(@($r.body.approvals) | Where-Object { $_.status -eq 'Pending' }).Count -eq 1) 'wf3 revision produced a NEW pending approval'
  Write-Host "  INFO  wf3 revision -> PendingManagerApproval (new proposal)" -ForegroundColor DarkCyan
} elseif ($r.body.status -eq 'Failed' -and $r.body.failureReason -match 'no_valid_slot') {
  Write-Host "  INFO  wf3 revision -> Failed/no_valid_slot (accepted)" -ForegroundColor DarkCyan
  $script:pass++
} else {
  Assert $false "wf3 after revision: unexpected status $($r.body.status) reason=$($r.body.failureReason)"
}

# =====================================================================
# Scheduling-agent priority rules (deterministic slot search):
#   1. exact preferred time, 2. nearest same-day window, 3. next date.
# =====================================================================

Step 'SCHEDULING A - preferred time taken -> nearest SAME-DAY window'
# Flip the 10:00 slot to Reserved (fixture-level - consultation creation
# only checks appointments, but the scheduling agent only books real
# 'Available' slot rows). A request for 10:00 must then stay on the
# preferred DATE and pick the nearest valid window (11:00).
RunSql "UPDATE ""AppointmentSlots"" SET ""Status""='Reserved', ""UpdatedAt""=now() WHERE ""VeterinarianId""='$vetId' AND ""Date""=DATE '$($WorkDate.ToString('yyyy-MM-dd'))' AND ""StartTime""=make_time(10,0,0)" | Out-Null
$consA = New-Consultation $petId $ownerProfileId $OrgId $owner.token 'Follow-up check, mild' '10:00:00'
$r = Call GET "/agent-workflows/by-consultation/$consA" $null $cm.token
$wfA = $r.body.id
QuotaPause
$r = Call POST "/agent-workflows/$wfA/run" $null $cm.token
Assert ($r.status -eq 200) 'wfA run -> 200'
if ($r.body.status -eq 'Failed') { Write-Host ("wfA FAILED: $($r.body.failureReason)") -ForegroundColor Red; throw "wfA failed: $($r.body.failureReason)" }
Assert ($r.body.status -eq 'PendingManagerApproval') "wfA PendingManagerApproval"
$apptA = $r.body.proposal.appointment
Assert ($null -ne $apptA) 'wfA proposal appointment present'
Assert ($apptA.date -eq $WorkDate.ToString('yyyy-MM-dd')) "wfA stayed on preferred date ($($apptA.date))"
Assert ($apptA.usedPreferredTime -eq $false) 'wfA usedPreferredTime=false'
Assert ($apptA.usedPreferredDate -eq $true) 'wfA usedPreferredDate=true'
Assert ($apptA.fallbackType -eq 'same_day_nearest_time') "wfA fallbackType=same_day_nearest_time ($($apptA.fallbackType))"
Assert (@($apptA.slotIds).Count -ge 1) "wfA proposal carries concrete slot ids ($(@($apptA.slotIds).Count))"
# Do NOT approve wfA - leave it pending so its slots are not consumed.

Step 'SCHEDULING B - preferred day exhausted -> NEAREST FUTURE DATE'
# Seed availability on WorkDate+5; ask for WorkDate+4 (nothing there).
$NextDay = $WorkDate.AddDays(5)
$PrefDay = $WorkDate.AddDays(4)
RunSql @"
INSERT INTO "AppointmentSlots" ("Id","VeterinarianId","Date","StartTime","EndTime","Branch","Status","CreatedAt","UpdatedAt")
SELECT gen_random_uuid(), '$vetId', DATE '$($NextDay.ToString('yyyy-MM-dd'))',
       make_time(h,0,0), make_time(h+1,0,0), 'Main', 'Available', now(), now()
FROM generate_series(9,12) AS h
"@ | Out-Null
$prefIso = $PrefDay.ToString('yyyy-MM-ddT10:00:00')
$r = Call POST '/consultations' @{ petId = $petId; ownerId = $ownerProfileId; symptoms = 'Routine check'; urgency = 'Low'; preferredDate = $prefIso; preferredTime = '10:00:00'; budget = 8000; additionalNotes = 'e2e next-day fallback'; organizationId = $OrgId } $owner.token
Assert ($r.status -eq 201) 'wfB consultation created'
$consB = $r.body.id
$r = Call POST "/consultations/$consB/submit" $null $owner.token
Assert ($r.status -eq 200) 'wfB consultation submitted'
$r = Call GET "/agent-workflows/by-consultation/$consB" $null $cm.token
$wfB = $r.body.id
QuotaPause
$r = Call POST "/agent-workflows/$wfB/run" $null $cm.token
Assert ($r.status -eq 200) 'wfB run -> 200'
if ($r.body.status -eq 'Failed') { Write-Host ("wfB FAILED: $($r.body.failureReason)") -ForegroundColor Red; throw "wfB failed: $($r.body.failureReason)" }
Assert ($r.body.status -eq 'PendingManagerApproval') "wfB PendingManagerApproval"
$apptB = $r.body.proposal.appointment
Assert ($null -ne $apptB) 'wfB proposal appointment present'
Assert ($apptB.date -eq $NextDay.ToString('yyyy-MM-dd')) "wfB picked next available date ($($apptB.date))"
Assert ($apptB.usedPreferredDate -eq $false) 'wfB usedPreferredDate=false'
Assert ($apptB.fallbackType -eq 'next_available_date') "wfB fallbackType=next_available_date ($($apptB.fallbackType))"

Step 'SCHEDULING C - no availability anywhere -> safe NO_VALID_SLOT'
# Preferred date beyond every seeded slot AND beyond the 30-day search
# window -> the workflow must fail safely, never invent availability.
$FarDay = $WorkDate.AddDays(45)
$prefIso = $FarDay.ToString('yyyy-MM-ddT10:00:00')
$r = Call POST '/consultations' @{ petId = $petId; ownerId = $ownerProfileId; symptoms = 'Routine check'; urgency = 'Low'; preferredDate = $prefIso; preferredTime = '10:00:00'; budget = 8000; additionalNotes = 'e2e no-slot failure'; organizationId = $OrgId } $owner.token
Assert ($r.status -eq 201) 'wfC consultation created'
$consC = $r.body.id
$r = Call POST "/consultations/$consC/submit" $null $owner.token
Assert ($r.status -eq 200) 'wfC consultation submitted'
$r = Call GET "/agent-workflows/by-consultation/$consC" $null $cm.token
$wfC = $r.body.id
QuotaPause
$r = Call POST "/agent-workflows/$wfC/run" $null $cm.token
Assert ($r.status -eq 200) 'wfC run -> 200'
Assert ($r.body.status -eq 'Failed') "wfC Failed ($($r.body.status))"
Assert ($r.body.failureReason -eq 'no_valid_slot') "wfC failureReason=no_valid_slot ($($r.body.failureReason))"
$schedStep = @($r.body.steps | Where-Object { $_.agentName -eq 'scheduling_agent' })
Assert ($schedStep.Count -eq 1 -and $schedStep[0].status -eq 'NoProposal') 'wfC scheduling step recorded NoProposal'
$out = $schedStep[0].output
Assert ($out.reasonCode -eq 'NO_VALID_SLOT') "wfC reasonCode=NO_VALID_SLOT ($($out.reasonCode))"

Step 'SCHEDULING D - consultation requiredSlots drives booking window'
# Trauma-style symptoms should yield a bounded multi-slot estimate from
# the consultation agent (deterministically clamped to 1-4). The test
# asserts consistency end-to-end: whatever the (bounded) estimate is,
# the proposal, approval and final booking all use that many consecutive
# slots and the booked appointment window spans slotCount hours.
$consD = New-Consultation $petId $ownerProfileId $OrgId $owner.token 'Hit by a vehicle, limping badly, cannot put weight on back leg' '13:00:00'
$r = Call GET "/agent-workflows/by-consultation/$consD" $null $cm.token
$wfD = $r.body.id
QuotaPause
$r = Call POST "/agent-workflows/$wfD/run" $null $cm.token
Assert ($r.status -eq 200) 'wfD run -> 200'
if ($r.body.status -eq 'Failed') { Write-Host ("wfD FAILED: $($r.body.failureReason)") -ForegroundColor Red; throw "wfD failed: $($r.body.failureReason)" }
Assert ($r.body.status -eq 'PendingManagerApproval') "wfD PendingManagerApproval"

$consStep = @($r.body.steps | Where-Object { $_.agentName -eq 'consultation_agent' })
Assert ($consStep.Count -eq 1 -and $consStep[0].status -eq 'Completed') 'wfD consultation step completed'
$need = $consStep[0].output.requiredSlots
Assert ($need -ge 1 -and $need -le 4) "wfD requiredSlots bounded 1-4 ($need)"
Write-Host "  INFO  wfD consultation assessment: complexity=$($consStep[0].output.complexity) requiredSlots=$need reason=$($consStep[0].output.schedulingReason)" -ForegroundColor DarkCyan

$apptD = $r.body.proposal.appointment
Assert ($null -ne $apptD) 'wfD proposal appointment present'
Assert ($apptD.slotCount -eq $need) "wfD proposal slotCount matches assessment ($($apptD.slotCount) == $need)"
Assert (@($apptD.slotIds).Count -eq $need) "wfD proposal carries $need slot ids"
Assert ($apptD.date -eq $WorkDate.ToString('yyyy-MM-dd')) 'wfD stayed on preferred date'

# Approve -> backend books the whole consecutive window transactionally.
$r = Call POST "/agent-workflows/$wfD/approve" @{ comments = 'approved e2e' } $cm.token
Assert ($r.status -eq 200 -and $r.body.status -eq 'AwaitingExamination') 'wfD approved -> AwaitingExamination'
$apptDId = RunSql "SELECT ""Id"" FROM ""Appointments"" WHERE ""ConsultationRequestId""='$consD'"
Assert ($apptDId) 'wfD appointment created'
$span = RunSql "SELECT EXTRACT(EPOCH FROM (""EndTime"" - ""StartTime""))/3600 FROM ""Appointments"" WHERE ""Id""='$apptDId'"
Assert ([int]$span -eq $need) "wfD appointment window spans $need hour(s) (actual: $span)"
$reserved = RunSql "SELECT count(*) FROM ""AppointmentSlots"" WHERE ""VeterinarianId""='$vetId' AND ""Status""='Reserved' AND ""Date""=DATE '$($apptD.date)' AND ""StartTime"" >= TIME '$($apptD.startTime)' AND ""StartTime"" < TIME '$($apptD.endTime)'"
Assert ([int]$reserved -eq $need) "wfD reserved its full $need-slot window ($reserved)"

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "E2E RESULT: $pass passed, $fail failed" -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) { exit 1 }
