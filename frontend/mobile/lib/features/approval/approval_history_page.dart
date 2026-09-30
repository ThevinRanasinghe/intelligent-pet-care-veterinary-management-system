import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
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
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: () => provider.loadHistory(widget.approvalId),
        );
      case LoadState.success:
        if (provider.history.isEmpty) {
          return const Center(child: Text('No history entries'));
        }
        return ListView.builder(
          itemCount: provider.history.length,
          itemBuilder: (context, index) {
            final h = provider.history[index];
            return AppCard(
              margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
              padding: EdgeInsets.zero,
              child: ListTile(
                leading: Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: AppColors.primarySoft,
                    borderRadius: BorderRadius.circular(11),
                  ),
                  child: const Icon(Icons.history,
                      color: AppColors.black, size: 20),
                ),
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
