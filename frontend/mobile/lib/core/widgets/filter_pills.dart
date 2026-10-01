import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import 'pressable_scale.dart';

/// One item in a [FilterPills] row. [emoji] renders a small leading glyph
/// (used for species icons on the medical-history pet filter).
class FilterPillItem {
  final String label;
  final String? emoji;

  const FilterPillItem(this.label, {this.emoji});
}

/// Beacon horizontal filter pills: selected = dark pill with light text;
/// unselected = white pill with muted text and a light border.
class FilterPills extends StatelessWidget {
  final List<FilterPillItem> items;
  final int selectedIndex;
  final ValueChanged<int> onSelected;

  const FilterPills({
    super.key,
    required this.items,
    required this.selectedIndex,
    required this.onSelected,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding:
            const EdgeInsets.symmetric(horizontal: AppSpacing.pageHorizontal),
        itemCount: items.length,
        separatorBuilder: (_, __) => const SizedBox(width: AppSpacing.xs),
        itemBuilder: (context, i) {
          final item = items[i];
          final selected = i == selectedIndex;
          return PressableScale(
            onTap: () => onSelected(i),
            child: AnimatedContainer(
              duration: AppDurations.step,
              padding: const EdgeInsets.symmetric(horizontal: 14),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: selected ? AppColors.black : AppColors.surface,
                borderRadius: BorderRadius.circular(AppRadius.pill),
                border: Border.all(
                  color: selected ? AppColors.black : AppColors.line,
                ),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (item.emoji != null) ...[
                    Text(item.emoji!, style: const TextStyle(fontSize: 14)),
                    const SizedBox(width: 6),
                  ],
                  Text(
                    item.label,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: selected ? Colors.white : AppColors.muted,
                    ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}
