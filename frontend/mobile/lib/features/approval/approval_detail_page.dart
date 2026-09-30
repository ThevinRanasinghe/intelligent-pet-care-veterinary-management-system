import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/status_badge.dart';
import 'approval_provider.dart';

class ApprovalDetailPage extends StatefulWidget {
  final String approvalId;

  const ApprovalDetailPage({super.key, required this.approvalId});

  @override
  State<ApprovalDetailPage> createState() => _ApprovalDetailPageState();
}

class _ApprovalDetailPageState extends State<ApprovalDetailPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ApprovalProvider>().loadApproval(widget.approvalId);
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ApprovalProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('Approval Details')),
      body: _buildBody(provider),
    );
  }

  Widget _buildBody(ApprovalProvider provider) {
    switch (provider.detailState) {
      case LoadState.idle:
      case LoadState.loading:
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: () => provider.loadApproval(widget.approvalId),
        );
      case LoadState.success:
        final a = provider.approval;
        if (a == null) {
          return const Center(child: Text('Approval not found'));
        }
        final withinBudget = a.quotationTotal <= a.quotationBudget;
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DetailRow('Approval ID', value: a.id, labelWidth: 140),
                  DetailRow('Quotation ID',
                      value: a.quotationId, labelWidth: 140),
                  DetailRow('Status',
                      labelWidth: 140, child: StatusBadge(a.status)),
                  DetailRow('Quotation Total',
                      value: 'LKR ${a.quotationTotal.toStringAsFixed(2)}',
                      labelWidth: 140),
                  DetailRow('Quotation Budget',
                      value: 'LKR ${a.quotationBudget.toStringAsFixed(2)}',
                      labelWidth: 140),
                  DetailRow('Within Budget',
                      value: withinBudget ? 'Yes' : 'No', labelWidth: 140),
                  if (a.reviewedBy != null)
                    DetailRow('Reviewed By',
                        value: a.reviewedBy!, labelWidth: 140),
                  if (a.reviewedAt != null)
                    DetailRow('Reviewed At',
                        value: a.reviewedAt!, labelWidth: 140),
                  if (a.comment != null && a.comment!.isNotEmpty)
                    DetailRow('Comment', value: a.comment!, labelWidth: 140),
                ],
              ),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              icon: const Icon(Icons.history),
              label: const Text('View History'),
              onPressed: () {
                Navigator.of(context)
                    .pushNamed('/approval-history', arguments: a.id);
              },
            ),
          ],
        );
    }
  }
}
