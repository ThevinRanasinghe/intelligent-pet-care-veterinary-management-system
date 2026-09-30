import 'package:flutter/material.dart';
import '../../features/pets/models/pet.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import 'app_card.dart';

/// Species emoji mirrors the web pet cards' illustrated pet avatars.
/// Used when the pet has no photoUrl.
String petSpeciesEmoji(String species) {
  switch (species.toLowerCase()) {
    case 'dog':
      return '🐶';
    case 'cat':
      return '🐱';
    case 'bird':
      return '🐦';
    case 'rabbit':
      return '🐰';
    default:
      return '🐾';
  }
}

/// Reusable white pet card: avatar (photoUrl or species emoji), name,
/// species · breed, age label, and a trailing action (default "View
/// Profile" that calls [onTap]).
class PetCard extends StatelessWidget {
  final Pet pet;
  final VoidCallback? onTap;

  const PetCard({super.key, required this.pet, this.onTap});

  @override
  Widget build(BuildContext context) {
    final hasPhoto = pet.photoUrl != null && pet.photoUrl!.isNotEmpty;
    return AppCard(
      margin: const EdgeInsets.symmetric(
          horizontal: AppSpacing.pageHorizontal, vertical: AppSpacing.xxs + 2),
      padding: EdgeInsets.zero,
      onTap: onTap,
      child: ListTile(
        leading: hasPhoto
            ? CircleAvatar(
                radius: 22,
                backgroundColor: AppColors.primarySoft,
                backgroundImage: NetworkImage(pet.photoUrl!),
              )
            : CircleAvatar(
                radius: 22,
                backgroundColor: AppColors.primarySoft,
                child: Text(
                  petSpeciesEmoji(pet.species),
                  style: const TextStyle(fontSize: 20),
                ),
              ),
        title: Text(pet.name),
        subtitle: Text(
          '${pet.species}${pet.breed != null && pet.breed!.isNotEmpty ? ' · ${pet.breed}' : ''}\n${pet.ageLabel}',
        ),
        isThreeLine: true,
        trailing: TextButton(
          onPressed: onTap,
          child: const Text('View Profile'),
        ),
      ),
    );
  }
}
