import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/motion/app_motion.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/top_bar.dart';
import '../auth/auth_provider.dart';
import 'billing_provider.dart';
import 'models/quotation.dart';

class QuotationsPage extends StatefulWidget {
  const QuotationsPage({super.key});

  @override
  State<QuotationsPage> createState() => _QuotationsPageState();
}

class _QuotationsPageState extends State<QuotationsPage> {
  /// PetOwner bills come from /quotations/mine; staff keep the org list.
  bool _mine(BuildContext context) =>
      context.read<AuthProvider>().session?.role == 'PetOwner';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<BillingProvider>().loadQuotations(mine: _mine(context));
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BillingProvider>();
    final mine = context.watch<AuthProvider>().session?.role == 'PetOwner';

    final body = RefreshIndicator(
      onRefresh: () => provider.loadQuotations(mine: mine),
      // Loading → list crossfades rather than snapping.
      child: AnimatedSwitcher(
        duration: AppMotion.standard,
        child: KeyedSubtree(
          key: ValueKey(provider.listState),
          child: _buildBody(provider, mine),
        ),
      ),
    );

    // Standalone (pushed) owner view gets its own scaffold + title; the
    // staff view stays embedded inside HomePage's scaffold.
    if (mine) {
      return Scaffold(
        appBar: const TopBar(title: 'My Bills', showBackButton: false),
        body: body,
      );
    }
    return body;
  }

  Widget _buildBody(BillingProvider provider, bool mine) {
    switch (provider.listState) {
      case LoadState.idle:
      case LoadState.loading:
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: () => provider.loadQuotations(mine: mine),
        );
      case LoadState.success:
        if (provider.quotations.isEmpty) {
          return ListView(
            children: [
              AppEmptyState(
                message: mine ? 'No bills yet' : 'No quotations found',
                icon: Icons.receipt_long,
                padding: const EdgeInsets.only(top: 200),
              ),
            ],
          );
        }
        return ListView.builder(
          itemCount: provider.quotations.length,
          itemBuilder: (context, index) {
            final q = provider.quotations[index];
            return FadeSlideIn(
              delay: Duration(milliseconds: 60 * index.clamp(0, 5)),
              distance: 10,
              child: QuotationTile(quotation: q),
            );
          },
        );
    }
  }
}

class QuotationTile extends StatelessWidget {
  final Quotation quotation;

  const QuotationTile({super.key, required this.quotation});

  @override
  Widget build(BuildContext context) {
    return AppCard(
      margin: const EdgeInsets.symmetric(
          horizontal: AppSpacing.pageHorizontal, vertical: AppSpacing.xxs + 2),
      padding: const EdgeInsets.all(AppSpacing.md),
      onTap: () {
        Navigator.of(context).pushNamed('/quotation', arguments: quotation.id);
      },
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const ListIconTile(icon: Icons.receipt_long),
              const SizedBox(width: AppSpacing.sm),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      quotation.invoiceNumber ??
                          'Quotation ${_shortId(quotation.id)}',
                      style: const TextStyle(
                        fontWeight: FontWeight.w700,
                        color: AppColors.black,
                      ),
                    ),
                    if (quotation.petName != null)
                      Text(
                        quotation.petName!,
                        style: const TextStyle(
                            fontSize: 12, color: AppColors.muted),
                      ),
                  ],
                ),
              ),
              StatusBadge(quotation.paymentStatus),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          const Divider(height: 1, color: AppColors.line),
          const SizedBox(height: AppSpacing.sm),
          Row(
            children: [
              StatusBadge(quotation.status),
              const SizedBox(width: 6),
              Icon(
                quotation.isWithinBudget ? Icons.check_circle : Icons.warning,
                color: quotation.isWithinBudget
                    ? AppColors.successText
                    : AppColors.warningText,
                size: 16,
              ),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  'Budget: LKR ${quotation.budget.toStringAsFixed(2)} · '
                  '${quotation.items.length} items',
                  style: const TextStyle(fontSize: 12, color: AppColors.muted),
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          // Beacon total: dark rounded box with the amount in yellow.
          Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md, vertical: AppSpacing.sm),
            decoration: BoxDecoration(
              color: AppColors.black,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                const Text(
                  'Total',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: Color(0xB3FFFFFF),
                  ),
                ),
                const Spacer(),
                Text(
                  'LKR ${quotation.total.toStringAsFixed(2)}',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: AppColors.primary,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  String _shortId(String id) => id.length > 8 ? '${id.substring(0, 8)}...' : id;
}
