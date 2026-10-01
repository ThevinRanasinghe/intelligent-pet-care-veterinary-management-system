import 'package:flutter/material.dart';
import '../theme/app_colors.dart';
import '../theme/app_text_styles.dart';

/// Beacon TopBar: translucent cream surface, thin bottom border, centred
/// ~18px extra-bold title, optional leading/back button and trailing
/// actions. Use as `appBar: const TopBar(title: '...')`.
class TopBar extends StatelessWidget implements PreferredSizeWidget {
  final String? title;
  final Widget? titleWidget;
  final Widget? leading;
  final List<Widget>? actions;

  /// When true (default) a back chevron is shown automatically whenever
  /// the navigator can pop. Pass `false` for root/tab pages.
  final bool showBackButton;

  const TopBar({
    super.key,
    this.title,
    this.titleWidget,
    this.leading,
    this.actions,
    this.showBackButton = true,
  });

  @override
  Size get preferredSize => const Size.fromHeight(56);

  @override
  Widget build(BuildContext context) {
    final canPop = Navigator.of(context).canPop();
    final effectiveLeading = leading ??
        (showBackButton && canPop
            ? _TopBarIconButton(
                icon: Icons.arrow_back_ios_new,
                tooltip: 'Back',
                onPressed: () => Navigator.of(context).maybePop(),
              )
            : null);

    return Container(
      decoration: const BoxDecoration(
        color: Color(0xF2FFFBEF), // cream, slightly translucent
        border: Border(bottom: BorderSide(color: AppColors.line)),
      ),
      child: SafeArea(
        bottom: false,
        child: SizedBox(
          height: 56,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8),
            child: Row(
              children: [
                SizedBox(
                  width: 48,
                  child: effectiveLeading,
                ),
                Expanded(
                  child: Center(
                    child: titleWidget ??
                        Text(
                          title ?? '',
                          style: AppTextStyles.pageTitle,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                  ),
                ),
                SizedBox(
                  width: 48,
                  child: actions != null && actions!.isNotEmpty
                      ? Row(
                          mainAxisAlignment: MainAxisAlignment.end,
                          mainAxisSize: MainAxisSize.min,
                          children: actions!,
                        )
                      : null,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _TopBarIconButton extends StatelessWidget {
  final IconData icon;
  final String tooltip;
  final VoidCallback onPressed;

  const _TopBarIconButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return IconButton(
      icon: Icon(icon, size: 18),
      color: AppColors.black,
      tooltip: tooltip,
      onPressed: onPressed,
    );
  }
}
