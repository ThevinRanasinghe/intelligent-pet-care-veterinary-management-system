import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/status_badge.dart';
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
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: provider.loadPendingApprovals,
        );
      case LoadState.success:
        if (provider.approvals.isEmpty) {
          return ListView(
            children: const [
              AppEmptyState(
                message: 'No pending approvals',
                icon: Icons.approval,
                padding: EdgeInsets.only(top: 200),
              ),
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
    return AppCard(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      padding: EdgeInsets.zero,
      onTap: () {
        Navigator.of(context).pushNamed('/approval', arguments: approval.id);
      },
      child: ListTile(
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.primarySoft,
            borderRadius: BorderRadius.circular(11),
          ),
          child:
              const Icon(Icons.approval, color: AppColors.black, size: 20),
        ),
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
            StatusBadge(approval.status),
            const SizedBox(width: 4),
            Icon(
              withinBudget ? Icons.check_circle : Icons.warning,
              color: withinBudget
                  ? AppColors.successText
                  : AppColors.warningText,
              size: 16,
            ),
          ],
        ),
      ),
    );
  }

  String _shortId(String id) => id.length > 8 ? '${id.substring(0, 8)}...' : id;
}
