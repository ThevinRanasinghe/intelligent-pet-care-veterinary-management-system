import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/step_dots.dart';
import '../home/main_shell.dart';
import '../pets/models/pet.dart';
import '../pets/pet_provider.dart';
import 'clinic_picker.dart';
import 'consultation_provider.dart';
import 'models/availability.dart';
import 'models/clinic.dart';
import 'models/consultation_request.dart';

/// Owner booking wizard: pet → clinic → date → time → details, then
/// POST /consultations + POST /consultations/{id}/submit.
class BookingWizardPage extends StatefulWidget {
  /// GoogleMap needs a configured Maps API key on device; widget tests
  /// pass false and exercise the identical list picker instead.
  final bool useMap;

  const BookingWizardPage({super.key, this.useMap = true});

  @override
  State<BookingWizardPage> createState() => _BookingWizardPageState();
}

class _BookingWizardPageState extends State<BookingWizardPage> {
  int _step = 0;
  Pet? _pet;
  Clinic? _clinic;
  String? _date; // yyyy-MM-dd
  String? _time; // HH:mm slot start
  final _symptomsController = TextEditingController();
  final _notesController = TextEditingController();
  ConsultationRequest? _submitted;

  late int _monthYear;
  late int _monthMonth;

  static const _stepTitles = [
    'Select Pet',
    'Select Clinic',
    'Select Date',
    'Select Time',
    'Details',
  ];

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    _monthYear = now.year;
    _monthMonth = now.month;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<PetProvider>().loadMyPets();
      context.read<ConsultationProvider>().loadClinics();
    });
  }

  @override
  void dispose() {
    _symptomsController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  bool get _canContinue {
    switch (_step) {
      case 0:
        return _pet != null;
      case 1:
        return _clinic != null;
      case 2:
        return _date != null;
      case 3:
        return _time != null;
      case 4:
        return _symptomsController.text.trim().isNotEmpty;
      default:
        return false;
    }
  }

  void _next() {
    if (!_canContinue) return;
    if (_step == 1) _loadMonth();
    setState(() => _step += 1);
  }

  void _back() {
    if (_step > 0) setState(() => _step--);
  }

  void _loadMonth() {
    final clinic = _clinic;
    if (clinic == null) return;
    context
        .read<ConsultationProvider>()
        .loadMonthAvailability(clinic.id, _monthYear, _monthMonth);
  }

  void _shiftMonth(int delta) {
    var month = _monthMonth + delta;
    var year = _monthYear;
    if (month < 1) {
      month = 12;
      year--;
    } else if (month > 12) {
      month = 1;
      year++;
    }
    setState(() {
      _monthYear = year;
      _monthMonth = month;
    });
    _loadMonth();
  }

  Future<void> _selectDate(String date) async {
    final clinic = _clinic;
    if (clinic == null) return;
    setState(() {
      _date = date;
      _time = null;
    });
    await context
        .read<ConsultationProvider>()
        .loadDayAvailability(clinic.id, date);
  }

  Future<void> _submit() async {
    if (!_canContinue || _pet == null || _clinic == null) return;
    final provider = context.read<ConsultationProvider>();
    final result = await provider.bookConsultation(
      petId: _pet!.id,
      ownerId: _pet!.ownerId,
      organizationId: _clinic!.id,
      preferredDate: _date!,
      preferredTime: _time!,
      symptoms: _symptomsController.text.trim(),
      additionalNotes: _notesController.text.trim(),
    );
    if (!mounted) return;
    if (result != null) {
      setState(() => _submitted = result);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(provider.submitError ?? 'Booking failed'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_submitted != null) return _buildSuccess();

    return Scaffold(
      appBar: AppBar(title: const Text('Book a Consultation')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            child: Row(
              children: [
                Text('Step ${_step + 1} of 5 — ${_stepTitles[_step]}',
                    style: const TextStyle(
                      fontWeight: FontWeight.w800,
                      color: AppColors.black,
                    )),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: StepDots(
                currentStep: _step,
                labels: const ['Pet', 'Clinic', 'Date', 'Time', 'Details']),
          ),
          const SizedBox(height: 8),
          Expanded(child: _buildStep()),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                children: [
                  if (_step > 0)
                    OutlinedButton(
                      onPressed: _back,
                      child: const Text('Back'),
                    ),
                  const Spacer(),
                  if (_step < 4)
                    FilledButton(
                      onPressed: _canContinue ? _next : null,
                      child: const Text('Next'),
                    )
                  else
                    Consumer<ConsultationProvider>(
                      builder: (context, provider, _) => FilledButton(
                        onPressed:
                            _canContinue && !provider.submitting ? _submit : null,
                        child: provider.submitting
                            ? const SizedBox(
                                height: 20,
                                width: 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Text('Submit Request'),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStep() {
    switch (_step) {
      case 0:
        return _buildPetStep();
      case 1:
        return _buildClinicStep();
      case 2:
        return _buildDateStep();
      case 3:
        return _buildTimeStep();
      default:
        return _buildDetailsStep();
    }
  }

  // ---- Step 1: pet ----------------------------------------------------

  Widget _buildPetStep() {
    final provider = context.watch<PetProvider>();
    switch (provider.listState) {
      case LoadState.idle:
      case LoadState.loading:
        return const Center(child: CircularProgressIndicator());
      case LoadState.error:
        return Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(provider.errorMessage, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: provider.loadMyPets,
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        if (provider.pets.isEmpty) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(24),
              child: Text(
                'You have no pets registered yet. Add a pet from the Pets tab first.',
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        return ListView.builder(
          padding: const EdgeInsets.all(8),
          itemCount: provider.pets.length,
          itemBuilder: (context, index) {
            final pet = provider.pets[index];
            final selected = _pet?.id == pet.id;
            return Card(
              color: selected ? AppColors.selectedBg : null,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(17),
                side: BorderSide(
                  color: selected ? AppColors.primaryDark : AppColors.line,
                ),
              ),
              child: ListTile(
                leading: Icon(
                  selected ? Icons.radio_button_checked : Icons.radio_button_off,
                  color: selected ? AppColors.primaryDark : AppColors.neutral,
                ),
                title: Text(pet.name),
                subtitle: Text(
                    '${pet.species}${pet.breed != null ? ' · ${pet.breed}' : ''}'),
                onTap: () => setState(() => _pet = pet),
              ),
            );
          },
        );
    }
  }

  // ---- Step 2: clinic ---------------------------------------------------

  Widget _buildClinicStep() {
    final provider = context.watch<ConsultationProvider>();
    switch (provider.clinicsState) {
      case LoadState.idle:
      case LoadState.loading:
        return const Center(child: CircularProgressIndicator());
      case LoadState.error:
        return Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Text('Unable to load clinics. Please try again.',
                  textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: provider.loadClinics,
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        return ClinicPicker(
          clinics: provider.clinics,
          selectedId: _clinic?.id,
          showMap: widget.useMap,
          onSelected: (clinic) => setState(() {
            _clinic = clinic;
            _date = null;
            _time = null;
          }),
        );
    }
  }

  // ---- Step 3: date -----------------------------------------------------

  Widget _buildDateStep() {
    final provider = context.watch<ConsultationProvider>();
    final byDate = {for (final d in provider.monthDays) d.date: d};
    final daysInMonth = DateUtils.getDaysInMonth(_monthYear, _monthMonth);
    final firstWeekday = DateTime(_monthYear, _monthMonth, 1).weekday;
    const monthNames = [
      'January', 'February', 'March', 'April', 'May', 'June', 'July',
      'August', 'September', 'October', 'November', 'December',
    ];

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              IconButton(
                icon: const Icon(Icons.chevron_left),
                onPressed: () => _shiftMonth(-1),
              ),
              Text('${monthNames[_monthMonth - 1]} $_monthYear',
                  style: const TextStyle(fontWeight: FontWeight.bold)),
              IconButton(
                icon: const Icon(Icons.chevron_right),
                onPressed: () => _shiftMonth(1),
              ),
            ],
          ),
        ),
        if (provider.monthState == LoadState.loading)
          const Expanded(
              child: Center(child: CircularProgressIndicator()))
        else if (provider.monthState == LoadState.error)
          Expanded(
            child: Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(provider.errorMessage, textAlign: TextAlign.center),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _loadMonth,
                    child: const Text('Retry'),
                  ),
                ],
              ),
            ),
          )
        else
          Expanded(
            child: GridView.builder(
              padding: const EdgeInsets.all(8),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 7,
                childAspectRatio: 0.72,
              ),
              itemCount: (firstWeekday - 1) + daysInMonth,
              itemBuilder: (context, index) {
                if (index < firstWeekday - 1) return const SizedBox.shrink();
                final day = index - (firstWeekday - 1) + 1;
                final dateStr =
                    '$_monthYear-${_monthMonth.toString().padLeft(2, '0')}-${day.toString().padLeft(2, '0')}';
                final info = byDate[dateStr];
                final enabled = info?.selectable ?? false;
                final selected = _date == dateStr;
                return InkWell(
                  key: Key('day-$dateStr'),
                  borderRadius: BorderRadius.circular(10),
                  onTap: enabled ? () => _selectDate(dateStr) : null,
                  child: Container(
                    margin: const EdgeInsets.all(2),
                    decoration: BoxDecoration(
                      // Selected = filled yellow; available = white card;
                      // past/fully-booked stay disabled and greyed out.
                      color: selected
                          ? AppColors.primary
                          : enabled
                              ? AppColors.surface
                              : AppColors.neutralBg,
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(
                        color: selected
                            ? AppColors.primaryDark
                            : enabled
                                ? AppColors.line
                                : AppColors.neutralBorder,
                      ),
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Text(
                          '$day',
                          style: TextStyle(
                            fontWeight: FontWeight.bold,
                            color: selected
                                ? AppColors.black
                                : enabled
                                    ? null
                                    : AppColors.neutral,
                          ),
                        ),
                        if (info?.fullyBooked == true)
                          const FittedBox(
                            child: Padding(
                              padding: EdgeInsets.symmetric(horizontal: 2),
                              child: Text(
                                'Fully booked',
                                style: TextStyle(
                                    fontSize: 9,
                                    color: AppColors.dangerText),
                              ),
                            ),
                          )
                        else if (info?.isPast == true)
                          const FittedBox(
                            child: Padding(
                              padding: EdgeInsets.symmetric(horizontal: 2),
                              child: Text(
                                'Past',
                                style: TextStyle(
                                    fontSize: 9, color: AppColors.neutral),
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
      ],
    );
  }

  // ---- Step 4: time -----------------------------------------------------

  Widget _buildTimeStep() {
    final provider = context.watch<ConsultationProvider>();
    switch (provider.dayState) {
      case LoadState.idle:
      case LoadState.loading:
        return const Center(child: CircularProgressIndicator());
      case LoadState.error:
        return Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(provider.errorMessage, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => provider.loadDayAvailability(
                    _clinic!.id, _date!),
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        final slots = provider.dayAvailability?.slots ?? const <AvailabilitySlot>[];
        if (slots.isEmpty) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.all(24),
              child: Text(
                'No appointment slots are available for this date.',
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            Text('Available times on $_date',
                style: const TextStyle(fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: slots.map((slot) {
                final selected = _time == slot.start;
                return ChoiceChip(
                  label: Text('${slot.start} – ${slot.end}'),
                  selected: selected,
                  onSelected: slot.available
                      ? (_) => setState(() => _time = slot.start)
                      : null,
                  disabledColor: AppColors.neutralBg,
                );
              }).toList(),
            ),
          ],
        );
    }
  }

  // ---- Step 5: details ---------------------------------------------------

  Widget _buildDetailsStep() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Booking Summary',
                  style: TextStyle(
                      fontWeight: FontWeight.w800, color: AppColors.black)),
              const SizedBox(height: 8),
              _summaryRow('Pet', _pet?.name ?? ''),
              _summaryRow('Clinic', _clinic?.name ?? ''),
              _summaryRow('Date', _date ?? ''),
              _summaryRow('Time', _time ?? ''),
            ],
          ),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _symptomsController,
          decoration: const InputDecoration(
            labelText: 'Symptoms *',
            hintText: 'Describe what is wrong with your pet',
          ),
          maxLines: 3,
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _notesController,
          decoration: const InputDecoration(
            labelText: 'Additional Notes',
          ),
          maxLines: 2,
          onChanged: (_) => setState(() {}),
        ),
      ],
    );
  }

  Widget _summaryRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        children: [
          SizedBox(
            width: 70,
            child: Text(label,
                style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 11,
                    color: AppColors.caption)),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }

  // ---- Success ------------------------------------------------------------

  Widget _buildSuccess() {
    final r = _submitted!;
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.check_circle,
                  size: 72, color: AppColors.successText),
              const SizedBox(height: 16),
              const Text(
                'Consultation Requested',
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -0.4,
                  color: AppColors.black,
                ),
              ),
              const SizedBox(height: 16),
              AppCard(
                child: Column(
                  children: [
                    _summaryRow('Pet', r.petName ?? _pet?.name ?? ''),
                    _summaryRow(
                        'Clinic', r.organizationName ?? _clinic?.name ?? ''),
                    _summaryRow(
                        'Date', r.preferredDateShort ?? _date ?? ''),
                    _summaryRow(
                        'Time', r.preferredTimeShort ?? _time ?? ''),
                    Padding(
                      padding: const EdgeInsets.only(bottom: 4),
                      child: Row(
                        children: [
                          const SizedBox(
                            width: 70,
                            child: Text('Status',
                                style: TextStyle(
                                    fontWeight: FontWeight.bold,
                                    fontSize: 11,
                                    color: AppColors.caption)),
                          ),
                          StatusBadge(r.status),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: () => MainShell.replaceWithTab(context, 2),
                child: const Text('View My Appointments'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
