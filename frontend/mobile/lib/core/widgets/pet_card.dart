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
/// Profile" that calls [onTap]). [grid] switches to the compact centred
/// layout used by the two-column Pets screen.
class PetCard extends StatelessWidget {
  final Pet pet;
  final VoidCallback? onTap;
  final bool grid;

  const PetCard({super.key, required this.pet, this.onTap, this.grid = false});

  @override
  Widget build(BuildContext context) {
    return grid ? _buildGrid(context) : _buildList(context);
  }

  Widget _avatar({required double size, double fontSize = 20}) {
    final hasPhoto = pet.photoUrl != null && pet.photoUrl!.isNotEmpty;
    if (hasPhoto) {
      return ClipRRect(
        borderRadius: BorderRadius.circular(AppRadius.card),
        child: Image.network(
          pet.photoUrl!,
          width: size,
          height: size,
          fit: BoxFit.cover,
          errorBuilder: (_, __, ___) => _emojiAvatar(size, fontSize),
        ),
      );
    }
    return _emojiAvatar(size, fontSize);
  }

  Widget _emojiAvatar(double size, double fontSize) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: AppColors.primarySoft,
        borderRadius: BorderRadius.circular(AppRadius.card),
      ),
      alignment: Alignment.center,
      child: Text(petSpeciesEmoji(pet.species),
          style: TextStyle(fontSize: fontSize)),
    );
  }

  /// Two-column grid card per the Beacon Pets spec: 64×64 avatar, name,
  /// species · breed, age, "View Profile". The avatar is Hero-tagged so
  /// it flies into the pet profile page's hero slot.
  Widget _buildGrid(BuildContext context) {
    final hasPhoto = pet.photoUrl != null && pet.photoUrl!.isNotEmpty;
    return AppCard(
      margin: EdgeInsets.zero,
      padding: const EdgeInsets.all(AppSpacing.sm),
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Hero(
            tag: 'pet-avatar-${pet.id}',
            child: hasPhoto
                ? _avatar(size: 64, fontSize: 28)
                : _emojiAvatar(64, 28),
          ),
          const SizedBox(height: AppSpacing.xs),
          Text(
            pet.name,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w800,
              color: AppColors.black,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            '${pet.species}${pet.breed != null && pet.breed!.isNotEmpty ? ' · ${pet.breed}' : ''}\n${pet.ageLabel}',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            textAlign: TextAlign.center,
            style: const TextStyle(fontSize: 11, color: AppColors.muted),
          ),
          const SizedBox(height: AppSpacing.xs),
          const Text(
            'View Profile',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w800,
              color: AppColors.primaryDark,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildList(BuildContext context) {
    final hasPhoto = pet.photoUrl != null && pet.photoUrl!.isNotEmpty;
    return AppCard(
      margin: const EdgeInsets.symmetric(
          horizontal: AppSpacing.pageHorizontal, vertical: AppSpacing.xxs + 2),
      padding: EdgeInsets.zero,
      onTap: onTap,
      child: ListTile(
        leading: hasPhoto
            ? ClipRRect(
                borderRadius: BorderRadius.circular(AppRadius.control),
                child: Image.network(
                  pet.photoUrl!,
                  width: 44,
                  height: 44,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => _emojiAvatar(44, 20),
                ),
              )
            : _emojiAvatar(44, 20),
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
