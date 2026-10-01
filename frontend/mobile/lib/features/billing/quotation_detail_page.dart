import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/motion/app_motion.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/section_header.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/top_bar.dart';
import 'billing_provider.dart';

class QuotationDetailPage extends StatefulWidget {
  final String quotationId;

  const QuotationDetailPage({super.key, required this.quotationId});

  @override
  State<QuotationDetailPage> createState() => _QuotationDetailPageState();
}

class _QuotationDetailPageState extends State<QuotationDetailPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<BillingProvider>().loadQuotation(widget.quotationId);
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BillingProvider>();

    return Scaffold(
      appBar: const TopBar(title: 'Quotation Details'),
      body: AnimatedSwitcher(
        duration: AppMotion.standard,
        child: KeyedSubtree(
          key: ValueKey(provider.detailState),
          child: _buildBody(provider),
        ),
      ),
    );
  }

  Widget _buildBody(BillingProvider provider) {
    switch (provider.detailState) {
      case LoadState.idle:
      case LoadState.loading:
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: () => provider.loadQuotation(widget.quotationId),
        );
      case LoadState.success:
        final q = provider.quotation;
        if (q == null) {
          return const Center(child: Text('Quotation not found'));
        }
        // Invoice-style layout: header card, meta details, then line
        // items and a divider-separated total.
        return ListView(
          padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
              AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
          children: [
            FadeSlideIn(
              child: AppCard(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            q.invoiceNumber ?? 'Quotation',
                            style: const TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w800,
                              letterSpacing: -0.4,
                              color: AppColors.black,
                            ),
                          ),
                        ),
                        StatusBadge(q.paymentStatus),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      'LKR ${q.total.toStringAsFixed(2)}'
                      '${q.paidAt != null ? ' · paid on ${q.paidAt!.substring(0, 10)}' : ''}',
                      style:
                          const TextStyle(fontSize: 13, color: AppColors.muted),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            const FadeSlideIn(
              delay: Duration(milliseconds: 80),
              distance: 8,
              child: SectionHeader('Details'),
            ),
            const SizedBox(height: 8),
            FadeSlideIn(
              delay: const Duration(milliseconds: 120),
              distance: 10,
              child: AppCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Invoice number is rendered once in the header card.
                    DetailRow('Quotation ID', value: q.id),
                    DetailRow('Appointment ID', value: q.appointmentId),
                    DetailRow('Status', child: StatusBadge(q.status)),
                    // Payment status already shown as the header badge
                    // (kept unique — tests assert a single 'Paid' text).
                    if (q.petName != null) DetailRow('Pet', value: q.petName!),
                    if (q.ownerName != null)
                      DetailRow('Owner', value: q.ownerName!),
                    if (q.veterinarianName != null)
                      DetailRow('Veterinarian', value: q.veterinarianName!),
                    if (q.examinationDate != null)
                      DetailRow('Examination', value: q.examinationDate!),
                    DetailRow('Vet charge',
                        value:
                            'LKR ${q.veterinarianChargeTotal.toStringAsFixed(2)}'),
                    DetailRow('Medicines',
                        value: 'LKR ${q.medicineTotal.toStringAsFixed(2)}'),
                    DetailRow('Budget',
                        value: 'LKR ${q.budget.toStringAsFixed(2)}'),
                    DetailRow('Subtotal',
                        value: 'LKR ${q.subtotal.toStringAsFixed(2)}'),
                    const Divider(height: 20),
                    // Beacon total: dark rounded box, amount in yellow.
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: AppSpacing.md, vertical: AppSpacing.sm),
                        decoration: BoxDecoration(
                          color: AppColors.black,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          children: [
                            const Text(
                              'Grand total',
                              style: TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.w700,
                                color: Color(0xB3FFFFFF),
                              ),
                            ),
                            const Spacer(),
                            Text(
                              'LKR ${q.total.toStringAsFixed(2)}',
                              style: const TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.w800,
                                color: AppColors.primary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    DetailRow('Within Budget',
                        value: q.isWithinBudget ? 'Yes' : 'No'),
                    DetailRow('Created', value: q.createdAt),
                    DetailRow('Updated', value: q.updatedAt),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            const FadeSlideIn(
              delay: Duration(milliseconds: 80),
              distance: 8,
              child: SectionHeader('Line Items'),
            ),
            const SizedBox(height: 8),
            for (var i = 0; i < q.items.length; i++)
              FadeSlideIn(
                delay: Duration(milliseconds: 120 + 50 * i.clamp(0, 6)),
                distance: 10,
                child: Builder(builder: (_) {
                  final item = q.items[i];
                  return AppCard(
                    margin: const EdgeInsets.only(bottom: 8),
                    padding: EdgeInsets.zero,
                    child: ListTile(
                      title: Text(item.description),
                      subtitle: Text(
                          '${item.category}  x${item.quantity}  @ LKR ${item.unitPrice.toStringAsFixed(2)}'),
                      trailing: Text(
                        'LKR ${item.totalPrice.toStringAsFixed(2)}',
                        style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            color: AppColors.black),
                      ),
                    ),
                  );
                }),
              ),
          ],
        );
    }
  }
}
