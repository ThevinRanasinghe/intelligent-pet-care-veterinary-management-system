import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/pet_card.dart' show petSpeciesEmoji;
import '../../core/widgets/section_header.dart';
import '../history/medical_history_page.dart';
import 'pet_provider.dart';
import 'models/pet.dart';
import 'pet_form_page.dart';

/// Owner pet profile: read view with Edit and Delete (confirm dialog).
class PetDetailPage extends StatelessWidget {
  final Pet pet;

  const PetDetailPage({super.key, required this.pet});

  Future<void> _confirmDelete(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Delete Pet'),
        content: Text('Delete ${pet.name}? This cannot be undone.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.danger,
              foregroundColor: Colors.white,
            ),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
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
    return Scaffold(
      appBar: AppBar(
        title: Text(pet.name),
        actions: [
          IconButton(
            icon: const Icon(Icons.edit),
            tooltip: 'Edit',
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => PetFormPage(pet: pet)),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.delete_outline),
            tooltip: 'Delete',
            color: AppColors.dangerText,
            onPressed: () => _confirmDelete(context),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Center(
            child: CircleAvatar(
              radius: 40,
              backgroundColor: AppColors.primarySoft,
              child: Text(
                petSpeciesEmoji(pet.species),
                style: const TextStyle(fontSize: 36),
              ),
            ),
          ),
          const SizedBox(height: 16),
          const SectionHeader('Pet Profile'),
          const SizedBox(height: 8),
          AppCard(
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
          const SizedBox(height: 16),
          OutlinedButton.icon(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(
                builder: (_) => MedicalHistoryPage(pet: pet),
              ),
            ),
            icon: const Icon(Icons.medical_information_outlined),
            label: const Text('Medical History'),
          ),
        ],
      ),
    );
  }
}
