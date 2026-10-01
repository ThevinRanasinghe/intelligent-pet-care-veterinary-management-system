import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/motion/app_motion.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/theme/app_text_styles.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_button.dart';
import '../../core/widgets/confirm_dialog.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/pet_card.dart' show petSpeciesEmoji;
import '../../core/widgets/section_header.dart';
import '../../core/widgets/top_bar.dart';
import '../history/medical_history_page.dart';
import 'pet_provider.dart';
import 'models/pet.dart';
import 'pet_form_sheet.dart';

/// Owner pet profile: 96px hero avatar, details card, Medical History
/// entry point and Edit/Delete actions (delete requires confirmation).
class PetDetailPage extends StatelessWidget {
  final Pet pet;

  const PetDetailPage({super.key, required this.pet});

  Future<void> _confirmDelete(BuildContext context) async {
    final confirmed = await showConfirmDialog(
      context,
      title: 'Delete Pet',
      message: 'Delete ${pet.name}? This cannot be undone.',
      confirmLabel: 'Delete',
      destructive: true,
    );
    if (!confirmed || !context.mounted) return;
    final error = await context.read<PetProvider>().deletePet(pet.id);
    if (!context.mounted) return;
    if (error == null) {
      Navigator.of(context).pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error), backgroundColor: AppColors.danger),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final hasPhoto = pet.photoUrl != null && pet.photoUrl!.isNotEmpty;
    return Scaffold(
      appBar: const TopBar(title: 'Pet Profile'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
            AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
        children: [
          // Hero: 96×96 avatar (shared-element flight from the pet
          // card's 64×64 avatar), then name/species fade in.
          const SizedBox(height: AppSpacing.sm),
          Center(
            child: Hero(
              tag: 'pet-avatar-${pet.id}',
              child: hasPhoto
                  ? ClipRRect(
                      borderRadius: BorderRadius.circular(AppRadius.large),
                      child: Image.network(
                        pet.photoUrl!,
                        width: 96,
                        height: 96,
                        fit: BoxFit.cover,
                        errorBuilder: (_, __, ___) => _emojiHero(),
                      ),
                    )
                  : _emojiHero(),
            ),
          ),
          const SizedBox(height: AppSpacing.sm),
          FadeSlideIn(
            delay: const Duration(milliseconds: 80),
            distance: 8,
            child: Center(
              child: Text(pet.name, style: AppTextStyles.display),
            ),
          ),
          FadeSlideIn(
            delay: const Duration(milliseconds: 120),
            distance: 8,
            child: Center(
              child: Text(
                '${pet.species}'
                '${pet.breed != null && pet.breed!.isNotEmpty ? ' · ${pet.breed}' : ''}',
                style: AppTextStyles.bodyMuted,
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.xl),
          const FadeSlideIn(
            delay: Duration(milliseconds: 160),
            distance: 8,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SectionHeader('Details'),
                SizedBox(height: AppSpacing.xs),
              ],
            ),
          ),
          FadeSlideIn(
            delay: const Duration(milliseconds: 200),
            distance: 10,
            child: AppCard.detail(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DetailRow('Name', value: pet.name),
                  DetailRow('Species', value: pet.species),
                  if (pet.breed != null && pet.breed!.isNotEmpty)
                    DetailRow('Breed', value: pet.breed!),
                  if (pet.gender != null)
                    DetailRow('Gender', value: pet.gender!),
                  DetailRow('Age', value: pet.ageLabel),
                  if (pet.dateOfBirth != null)
                    DetailRow('Date of Birth',
                        value: pet.dateOfBirth!.substring(0, 10)),
                  if (pet.weight != null)
                    DetailRow('Weight',
                        value: '${pet.weight!.toStringAsFixed(1)} kg'),
                  if (pet.notes != null && pet.notes!.isNotEmpty)
                    DetailRow('Notes', value: pet.notes!),
                ],
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.md),
          FadeSlideIn(
            delay: const Duration(milliseconds: 250),
            distance: 10,
            child: AppCard(
              padding: EdgeInsets.zero,
              onTap: () => Navigator.of(context).push(
                MotionPageRoute(page: MedicalHistoryPage(pet: pet)),
              ),
              child: const ListTile(
                leading: ListIconTile(
                    icon: Icons.medical_information_outlined, size: 36),
                title: Text(
                  'Medical History',
                  style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                ),
                trailing: Icon(Icons.chevron_right, color: AppColors.caption),
              ),
            ),
          ),
          const SizedBox(height: AppSpacing.lg),
          FadeSlideIn(
            delay: const Duration(milliseconds: 300),
            distance: 8,
            child: Row(
              children: [
                Expanded(
                  child: AppButton(
                    label: 'Edit',
                    icon: Icons.edit_outlined,
                    variant: AppButtonVariant.secondary,
                    onPressed: () => showPetFormSheet(context, pet: pet),
                  ),
                ),
                const SizedBox(width: AppSpacing.sm),
                Expanded(
                  child: AppButton(
                    label: 'Delete',
                    icon: Icons.delete_outline,
                    variant: AppButtonVariant.destructive,
                    onPressed: () => _confirmDelete(context),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _emojiHero() {
    return Container(
      width: 96,
      height: 96,
      decoration: BoxDecoration(
        color: AppColors.primarySoft,
        borderRadius: BorderRadius.circular(AppRadius.large),
      ),
      alignment: Alignment.center,
      child: Text(petSpeciesEmoji(pet.species),
          style: const TextStyle(fontSize: 44)),
    );
  }
}
