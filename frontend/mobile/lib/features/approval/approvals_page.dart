import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'approval_provider.dart';
import 'models/approval.dart';

class ApprovalsPage extends StatefulWidget {
  const ApprovalsPage({super.key});

  @override
  State<ApprovalsPage> createState() => _ApprovalsPageState();
}

class _ApprovalsPageState extends State<ApprovalsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ApprovalProvider>().loadPendingApprovals();
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ApprovalProvider>();

    return RefreshIndicator(
      onRefresh: provider.loadPendingApprovals,
      child: _buildBody(provider),
    );
  }

  Widget _buildBody(ApprovalProvider provider) {
    switch (provider.listState) {
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
              FilledButton(onPressed: provider.loadPendingApprovals, child: const Text('Retry')),
            ],
          ),
        );
      case LoadState.success:
        if (provider.approvals.isEmpty) {
          return ListView(
            children: const [
              SizedBox(height: 200),
              Center(child: Text('No pending approvals')),
            ],
          );
        }
        return ListView.builder(
          itemCount: provider.approvals.length,
          itemBuilder: (context, index) {
            final a = provider.approvals[index];
            return ApprovalTile(approval: a);
          },
        );
    }
  }
}

class ApprovalTile extends StatelessWidget {
  final Approval approval;

  const ApprovalTile({super.key, required this.approval});

  @override
  Widget build(BuildContext context) {
    final withinBudget = approval.quotationTotal <= approval.quotationBudget;
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: ListTile(
        leading: const CircleAvatar(child: Icon(Icons.approval)),
        title: Text('Approval ${_shortId(approval.id)}'),
        subtitle: Text(
          'Quotation: ${_shortId(approval.quotationId)}\n'
          'Total: LKR ${approval.quotationTotal.toStringAsFixed(2)}\n'
          'Budget: LKR ${approval.quotationBudget.toStringAsFixed(2)}',
        ),
        isThreeLine: true,
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Chip(label: Text(approval.status)),
            const SizedBox(width: 4),
            Icon(
              withinBudget ? Icons.check_circle : Icons.warning,
              color: withinBudget ? Colors.green : Colors.orange,
              size: 16,
            ),
          ],
        ),
        onTap: () {
          Navigator.of(context).pushNamed('/approval', arguments: approval.id);
        },
      ),
    );
  }

  String _shortId(String id) => id.length > 8 ? '${id.substring(0, 8)}...' : id;
}
