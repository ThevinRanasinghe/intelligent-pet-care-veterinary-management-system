import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/theme/app_text_styles.dart';
import '../../core/motion/app_motion.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/filter_pills.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/pet_card.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/top_bar.dart';
import '../pets/models/pet.dart';
import '../pets/pet_provider.dart';
import 'history_provider.dart';
import 'models/medical_history_entry.dart';

/// Pet Owner "Medical History": a vertical timeline of a pet's
/// examinations, each expandable into diagnosis → treatment →
/// prescriptions. Reachable from Home quick actions, a pet's profile
/// page and the Profile menu. Pass [pet] to deep-link one pet; without
/// it a pet selector row is shown.
class MedicalHistoryPage extends StatefulWidget {
  final Pet? pet;

  const MedicalHistoryPage({super.key, this.pet});

  @override
  State<MedicalHistoryPage> createState() => _MedicalHistoryPageState();
}

class _MedicalHistoryPageState extends State<MedicalHistoryPage> {
  Pet? _selectedPet;

  @override
  void initState() {
    super.initState();
    _selectedPet = widget.pet;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_selectedPet != null) {
        context.read<HistoryProvider>().loadForPet(_selectedPet!.id);
      } else {
        // Load the pet list so the selector row can render.
        context.read<PetProvider>().loadMyPets();
      }
    });
  }

  void _selectPet(Pet pet) {
    if (_selectedPet?.id == pet.id) return;
    setState(() => _selectedPet = pet);
    context.read<HistoryProvider>().loadForPet(pet.id);
  }

  @override
  Widget build(BuildContext context) {
    final history = context.watch<HistoryProvider>();
    final pets = context.watch<PetProvider>();

    return Scaffold(
      appBar: const TopBar(title: 'Medical History'),
      body: RefreshIndicator(
        onRefresh: () async {
          final pet = _selectedPet;
          if (pet != null) {
            await history.loadForPet(pet.id, force: true);
          } else {
            await pets.loadMyPets();
          }
        },
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.only(bottom: AppSpacing.xl),
          children: [
            if (widget.pet == null)
              _PetSelector(
                // Archived pets stay selectable so their history can be viewed.
                pets: pets.allPets,
                loading: pets.listState == LoadState.loading,
                selected: _selectedPet,
                onSelected: _selectPet,
              ),
            // Pet-filter and load-state swaps crossfade — no page flash.
            AnimatedSwitcher(
              duration: AppMotion.standard,
              child: KeyedSubtree(
                key: ValueKey('${_selectedPet?.id ?? 'none'}-${history.state}'),
                child: _buildTimeline(history),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildTimeline(HistoryProvider history) {
    if (_selectedPet == null) {
      return const AppEmptyState(
        message: 'Select a pet to see its medical history',
        icon: Icons.medical_information_outlined,
      );
    }
    switch (history.state) {
      case LoadState.idle:
      case LoadState.loading:
        return const Padding(
          padding: EdgeInsets.only(top: 120),
          child: AppLoading(),
        );
      case LoadState.error:
        return Padding(
          padding: const EdgeInsets.only(top: 120),
          child: AppErrorState(
            message: history.errorMessage,
            onRetry: () => history.loadForPet(_selectedPet!.id, force: true),
          ),
        );
      case LoadState.success:
        if (history.entries.isEmpty) {
          return const AppEmptyState(
            message: 'No medical records yet',
            hint: 'Examinations, diagnoses and prescriptions will appear here.',
            icon: Icons.medical_information_outlined,
          );
        }
        return Column(
          children: [
            for (var i = 0; i < history.entries.length; i++)
              FadeSlideIn(
                delay: Duration(milliseconds: 60 * i.clamp(0, 6)),
                distance: 10,
                child: _HistoryTimelineItem(
                  entry: history.entries[i],
                  isLast: i == history.entries.length - 1,
                  delayIndex: i,
                ),
              ),
          ],
        );
    }
  }
}

class _PetSelector extends StatelessWidget {
  final List<Pet> pets;
  final bool loading;
  final Pet? selected;
  final ValueChanged<Pet> onSelected;

  const _PetSelector({
    required this.pets,
    required this.loading,
    required this.selected,
    required this.onSelected,
  });

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Padding(
        padding: EdgeInsets.all(AppSpacing.md),
        child: AppLoading(),
      );
    }
    if (pets.isEmpty) {
      return const AppEmptyState(
        message: 'No pets registered yet',
        icon: Icons.pets,
      );
    }
    // Beacon filter pills: selected = dark pill, unselected = white.
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
      child: FilterPills(
        items: [
          for (final pet in pets)
            FilterPillItem(pet.name, emoji: petSpeciesEmoji(pet.species)),
        ],
        selectedIndex: selected == null
            ? -1
            : pets.indexWhere((p) => p.id == selected!.id),
        onSelected: (i) => onSelected(pets[i]),
      ),
    );
  }
}

/// One examination node on the vertical timeline: date + 🩺 icon +
/// symptoms, with nested Diagnosis / Treatment / Prescription sections.
class _HistoryTimelineItem extends StatelessWidget {
  final MedicalHistoryEntry entry;
  final bool isLast;
  final int delayIndex;

  const _HistoryTimelineItem({
    required this.entry,
    required this.isLast,
    this.delayIndex = 0,
  });

  static const _months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec',
  ];

  String get _dateLabel {
    final d = entry.examinationDate;
    return '${d.day} ${_months[d.month - 1]} ${d.year}';
  }

  @override
  Widget build(BuildContext context) {
    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Timeline rail: icon + connecting line.
          Padding(
            padding: const EdgeInsets.only(left: AppSpacing.pageHorizontal),
            child: Column(
              children: [
                const Padding(
                  padding: EdgeInsets.only(top: AppSpacing.md),
                  child: ListIconTile(icon: Icons.medical_services_outlined),
                ),
                if (!isLast)
                  Expanded(
                    child: Container(
                      width: 2,
                      margin:
                          const EdgeInsets.symmetric(vertical: AppSpacing.xxs),
                      color: AppColors.line,
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: AppSpacing.sm),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(
                  right: AppSpacing.pageHorizontal, bottom: AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(
                        top: AppSpacing.md + 2, bottom: AppSpacing.xs),
                    child: Text(
                      '$_dateLabel · Examination',
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.muted,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  AppCard(
                    margin: EdgeInsets.zero,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          entry.symptoms.isNotEmpty
                              ? entry.symptoms
                              : 'Routine examination',
                          style: AppTextStyles.cardTitle,
                        ),
                        if (entry.notes.isNotEmpty) ...[
                          const SizedBox(height: AppSpacing.xxs),
                          Text(entry.notes, style: AppTextStyles.bodyMuted),
                        ],
                        for (final d in entry.diagnoses) ...[
                          const Divider(height: AppSpacing.lg),
                          _DiagnosisBlock(diagnosis: d),
                        ],
                      ],
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
}

/// Clinical detail block with the strong left accent line the Beacon spec
/// calls for (diagnosis → treatment → prescription).
class _DiagnosisBlock extends StatelessWidget {
  final HistoryDiagnosis diagnosis;

  const _DiagnosisBlock({required this.diagnosis});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.only(left: AppSpacing.sm),
      decoration: const BoxDecoration(
        border: Border(
          left: BorderSide(color: AppColors.primaryDark, width: 3),
        ),
      ),
      child: _buildContent(),
    );
  }

  Widget _buildContent() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Diagnosis: ${diagnosis.conditionName}',
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
              ),
            ),
            if (diagnosis.severity.isNotEmpty) StatusBadge(diagnosis.severity),
          ],
        ),
        if (diagnosis.description.isNotEmpty) ...[
          const SizedBox(height: AppSpacing.xxs),
          Text(diagnosis.description, style: AppTextStyles.bodyMuted),
        ],
        for (final t in diagnosis.treatments)
          Padding(
            padding: const EdgeInsets.only(top: AppSpacing.sm),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'Treatment: ${t.procedureName}',
                        style: AppTextStyles.body
                            .copyWith(fontWeight: FontWeight.w600),
                      ),
                    ),
                    if (t.status.isNotEmpty) StatusBadge(t.status),
                  ],
                ),
                for (final rx in t.prescriptions)
                  Padding(
                    padding: const EdgeInsets.only(top: AppSpacing.xxs),
                    child: _PrescriptionRow(rx: rx),
                  ),
              ],
            ),
          ),
      ],
    );
  }
}

class _PrescriptionRow extends StatelessWidget {
  final HistoryPrescription rx;

  const _PrescriptionRow({required this.rx});

  @override
  Widget build(BuildContext context) {
    final name = rx.medicineName ?? 'Medicine';
    final strength =
        rx.medicineStrength != null ? ' ${rx.medicineStrength}' : '';
    final duration = rx.durationDays > 0 ? ' · ${rx.durationDays} days' : '';
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Icon(Icons.medication_outlined, size: 16, color: AppColors.muted),
        const SizedBox(width: AppSpacing.xs),
        Expanded(
          child: Text(
            '$name$strength — ${rx.dosage}$duration',
            style: AppTextStyles.bodyMuted,
          ),
        ),
      ],
    );
  }
}
