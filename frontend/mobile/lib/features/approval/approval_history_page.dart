import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'approval_provider.dart';

class ApprovalHistoryPage extends StatefulWidget {
  final String approvalId;

  const ApprovalHistoryPage({super.key, required this.approvalId});

  @override
  State<ApprovalHistoryPage> createState() => _ApprovalHistoryPageState();
}

class _ApprovalHistoryPageState extends State<ApprovalHistoryPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ApprovalProvider>().loadHistory(widget.approvalId);
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<ApprovalProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('Approval History')),
      body: _buildBody(provider),
    );
  }

  Widget _buildBody(ApprovalProvider provider) {
    switch (provider.historyState) {
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
                onPressed: () => provider.loadHistory(widget.approvalId),
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        if (provider.history.isEmpty) {
          return const Center(child: Text('No history entries'));
        }
        return ListView.builder(
          itemCount: provider.history.length,
          itemBuilder: (context, index) {
            final h = provider.history[index];
            return Card(
              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              child: ListTile(
                leading: const CircleAvatar(child: Icon(Icons.history)),
                title: Text('${h.previousStatus} -> ${h.newStatus}'),
                subtitle: Text(
                  'By: ${h.changedBy.substring(0, 8)}...\n'
                  'At: ${h.changedAt}'
                  '${h.reason != null ? '\nReason: ${h.reason}' : ''}',
                ),
                isThreeLine: true,
              ),
            );
          },
        );
    }
  }
}
