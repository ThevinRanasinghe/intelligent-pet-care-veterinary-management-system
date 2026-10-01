import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/motion/app_motion.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_text_field.dart';
import '../../core/widgets/booking_calendar.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/pet_card.dart' show petSpeciesEmoji;
import '../../core/widgets/pop_in.dart';
import '../../core/widgets/selectable_card.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/step_dots.dart';
import '../../core/widgets/time_slot_grid.dart';
import '../../core/widgets/top_bar.dart';
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

  /// +1 after NEXT, −1 after BACK — the step AnimatedSwitcher uses this
  /// to slide the new content in the matching direction.
  int _stepDirection = 1;
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
    setState(() {
      _stepDirection = 1;
      _step += 1;
    });
  }

  void _back() {
    if (_step > 0) {
      setState(() {
        _stepDirection = -1;
        _step--;
      });
    }
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
      appBar: const TopBar(title: 'Book a Visit'),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.pageHorizontal, vertical: 8),
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
            padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.pageHorizontal),
            child: StepDots(
                currentStep: _step,
                labels: const ['Pet', 'Clinic', 'Date', 'Time', 'Details']),
          ),
          const SizedBox(height: 8),
          // Directional step swap: NEXT slides the old step left and the
          // new step in from the right; BACK reverses the direction.
          // Only the content area animates — selections/form state are
          // preserved in this State object.
          Expanded(
            child: AnimatedSwitcher(
              duration: AppMotion.standard,
              switchInCurve: AppMotion.easeOut,
              switchOutCurve: AppMotion.easeOut,
              transitionBuilder: (child, animation) {
                final incoming = child.key == ValueKey<int>(_step);
                final begin =
                    Offset(0.05 * _stepDirection * (incoming ? 1 : -1), 0);
                return FadeTransition(
                  opacity: animation,
                  child: SlideTransition(
                    position: Tween(begin: begin, end: Offset.zero).animate(
                      animation,
                    ),
                    child: child,
                  ),
                );
              },
              child: KeyedSubtree(
                key: ValueKey<int>(_step),
                child: _buildStep(),
              ),
            ),
          ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.pageHorizontal, vertical: 12),
              child: Row(
                children: [
                  if (_step > 0)
                    OutlinedButton.icon(
                      onPressed: _back,
                      icon: const Icon(Icons.arrow_back, size: 16),
                      label: const Text('Back'),
                    ),
                  const Spacer(),
                  if (_step < 4)
                    FilledButton.icon(
                      onPressed: _canContinue ? _next : null,
                      icon: const Icon(Icons.arrow_forward, size: 16),
                      iconAlignment: IconAlignment.end,
                      label: const Text('Next'),
                    )
                  else
                    Consumer<ConsultationProvider>(
                      builder: (context, provider, _) => FilledButton(
                        onPressed: _canContinue && !provider.submitting
                            ? _submit
                            : null,
                        child: provider.submitting
                            ? const SizedBox(
                                height: 20,
                                width: 20,
                                child:
                                    CircularProgressIndicator(strokeWidth: 2),
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
        // Full-width selectable cards: unselected = white card; selected =
        // soft-yellow fill + yellow ring + popping check (SelectableCard).
        return ListView.builder(
          padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.pageHorizontal, vertical: 8),
          itemCount: provider.pets.length,
          itemBuilder: (context, index) {
            final pet = provider.pets[index];
            final selected = _pet?.id == pet.id;
            return FadeSlideIn(
              delay: Duration(milliseconds: 50 * index.clamp(0, 6)),
              distance: 8,
              child: SelectableCard(
                margin: const EdgeInsets.only(bottom: AppSpacing.xs),
                selected: selected,
                onTap: () => setState(() => _pet = pet),
                child: Row(
                  children: [
                    Container(
                      width: 48,
                      height: 48,
                      decoration: BoxDecoration(
                        color: AppColors.primarySoft,
                        borderRadius: BorderRadius.circular(AppRadius.control),
                      ),
                      alignment: Alignment.center,
                      child: Text(petSpeciesEmoji(pet.species),
                          style: const TextStyle(fontSize: 22)),
                    ),
                    const SizedBox(width: AppSpacing.sm),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            pet.name,
                            style: const TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.w800,
                              color: AppColors.black,
                            ),
                          ),
                          Text(
                            '${pet.species}${pet.breed != null ? ' · ${pet.breed}' : ''}',
                            style: const TextStyle(
                                fontSize: 12, color: AppColors.muted),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
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
    final byDate = {
      for (final d in provider.monthDays)
        d.date: CalendarDay(
          selectable: d.selectable,
          fullyBooked: d.fullyBooked,
          isPast: d.isPast,
        ),
    };

    return Column(
      children: [
        if (provider.monthState == LoadState.loading)
          const Expanded(child: Center(child: CircularProgressIndicator()))
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
            child: BookingCalendar(
              year: _monthYear,
              month: _monthMonth,
              days: byDate,
              selectedDate: _date,
              onSelect: _selectDate,
              onShiftMonth: _shiftMonth,
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
                onPressed: () =>
                    provider.loadDayAvailability(_clinic!.id, _date!),
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        final slots =
            provider.dayAvailability?.slots ?? const <AvailabilitySlot>[];
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
        // Beacon two-column time grid: white = available, yellow =
        // selected, muted struck = unavailable.
        return ListView(
          padding: const EdgeInsets.symmetric(
              horizontal: AppSpacing.pageHorizontal, vertical: 16),
          children: [
            Text('Available times on $_date',
                style: const TextStyle(
                    fontWeight: FontWeight.w700, color: AppColors.black)),
            const SizedBox(height: 12),
            TimeSlotGrid(
              slots: [
                for (final slot in slots)
                  TimeSlot(
                    label: '${slot.start} – ${slot.end}',
                    value: slot.start,
                    available: slot.available,
                  ),
              ],
              selected: _time,
              onSelected: (v) => setState(() => _time = v),
            ),
          ],
        );
    }
  }

  // ---- Step 5: details ---------------------------------------------------

  Widget _buildDetailsStep() {
    return ListView(
      padding: const EdgeInsets.symmetric(
          horizontal: AppSpacing.pageHorizontal, vertical: 16),
      children: [
        FadeSlideIn(
          child: AppCard.detail(
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
        ),
        const SizedBox(height: 16),
        FadeSlideIn(
          delay: const Duration(milliseconds: 80),
          child: AppTextField(
            label: 'Symptoms *',
            hint: 'Describe what is wrong with your pet',
            controller: _symptomsController,
            maxLines: 3,
            onChanged: (_) => setState(() {}),
          ),
        ),
        const SizedBox(height: 16),
        FadeSlideIn(
          delay: const Duration(milliseconds: 140),
          child: AppTextField(
            label: 'Additional Notes',
            hint: 'Anything else we should know?',
            controller: _notesController,
            maxLines: 2,
            onChanged: (_) => setState(() {}),
          ),
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
    // Rewarding but professional sequence: icon pops (0.5 → 1.08 → 1)
    // over a soft radial glow, then title → summary card → CTA stagger.
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Stack(
                alignment: Alignment.center,
                children: [
                  PopIn(
                    beginScale: 0.4,
                    peakScale: 1.15,
                    duration: AppMotion.emphasis,
                    child: Container(
                      width: 112,
                      height: 112,
                      decoration: const BoxDecoration(
                        shape: BoxShape.circle,
                        color: AppColors.successBg,
                      ),
                    ),
                  ),
                  const PopIn(
                    beginScale: 0.5,
                    peakScale: 1.08,
                    delay: Duration(milliseconds: 90),
                    child: Icon(Icons.check_circle,
                        size: 72, color: AppColors.successText),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              const FadeSlideIn(
                delay: Duration(milliseconds: 200),
                distance: 12,
                child: Text(
                  'Consultation Requested',
                  style: TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    letterSpacing: -0.4,
                    color: AppColors.black,
                  ),
                ),
              ),
              const SizedBox(height: 16),
              FadeSlideIn(
                delay: const Duration(milliseconds: 300),
                distance: 12,
                child: AppCard(
                  child: Column(
                    children: [
                      _summaryRow('Pet', r.petName ?? _pet?.name ?? ''),
                      _summaryRow(
                          'Clinic', r.organizationName ?? _clinic?.name ?? ''),
                      _summaryRow('Date', r.preferredDateShort ?? _date ?? ''),
                      _summaryRow('Time', r.preferredTimeShort ?? _time ?? ''),
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
              ),
              const SizedBox(height: 24),
              FadeSlideIn(
                delay: const Duration(milliseconds: 400),
                distance: 10,
                child: FilledButton(
                  onPressed: () => MainShell.replaceWithTab(context, 2),
                  child: const Text('View My Appointments'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
