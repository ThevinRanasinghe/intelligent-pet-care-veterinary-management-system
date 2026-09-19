import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
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
        return const Center(child: CircularProgressIndicator());
      case LoadState.error:
        return Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(Icons.error_outline, size: 48, color: Colors.red),
              const SizedBox(height: 8),
              Text(provider.errorMessage, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => provider.loadApproval(widget.approvalId),
                child: const Text('Retry'),
              ),
            ],
          ),
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
            _row('Approval ID', a.id),
            _row('Quotation ID', a.quotationId),
            _row('Status', a.status),
            _row('Quotation Total', 'LKR ${a.quotationTotal.toStringAsFixed(2)}'),
            _row('Quotation Budget', 'LKR ${a.quotationBudget.toStringAsFixed(2)}'),
            _row('Within Budget', withinBudget ? 'Yes' : 'No'),
            if (a.reviewedBy != null) _row('Reviewed By', a.reviewedBy!),
            if (a.reviewedAt != null) _row('Reviewed At', a.reviewedAt!),
            if (a.comment != null && a.comment!.isNotEmpty)
              _row('Comment', a.comment!),
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

  Widget _row(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 140,
            child: Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}
