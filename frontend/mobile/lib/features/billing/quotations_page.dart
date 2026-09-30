import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/status_badge.dart';
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
      child: _buildBody(provider, mine),
    );

    // Standalone (pushed) owner view gets its own scaffold + title; the
    // staff view stays embedded inside HomePage's scaffold.
    if (mine) {
      return Scaffold(
        appBar: AppBar(title: const Text('My Bills')),
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
            return QuotationTile(quotation: q);
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
              Expanded(
                child: Text(
                  'Total: LKR ${quotation.total.toStringAsFixed(2)}',
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w800,
                    color: AppColors.black,
                  ),
                ),
              ),
              StatusBadge(quotation.status),
              const SizedBox(width: 4),
              Icon(
                quotation.isWithinBudget
                    ? Icons.check_circle
                    : Icons.warning,
                color: quotation.isWithinBudget
                    ? AppColors.successText
                    : AppColors.warningText,
                size: 16,
              ),
            ],
          ),
          const SizedBox(height: 2),
          Text(
            'Budget: LKR ${quotation.budget.toStringAsFixed(2)}\n'
            'Items: ${quotation.items.length}',
            style:
                const TextStyle(fontSize: 12, color: AppColors.muted),
          ),
        ],
      ),
    );
  }

  String _shortId(String id) => id.length > 8 ? '${id.substring(0, 8)}...' : id;
}
