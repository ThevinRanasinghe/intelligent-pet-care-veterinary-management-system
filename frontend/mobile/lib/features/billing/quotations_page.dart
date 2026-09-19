import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'billing_provider.dart';
import 'models/quotation.dart';

class QuotationsPage extends StatefulWidget {
  const QuotationsPage({super.key});

  @override
  State<QuotationsPage> createState() => _QuotationsPageState();
}

class _QuotationsPageState extends State<QuotationsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<BillingProvider>().loadQuotations();
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BillingProvider>();

    return RefreshIndicator(
      onRefresh: provider.loadQuotations,
      child: _buildBody(provider),
    );
  }

  Widget _buildBody(BillingProvider provider) {
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
              FilledButton(onPressed: provider.loadQuotations, child: const Text('Retry')),
            ],
          ),
        );
      case LoadState.success:
        if (provider.quotations.isEmpty) {
          return ListView(
            children: const [
              SizedBox(height: 200),
              Center(child: Text('No quotations found')),
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
    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      child: ListTile(
        leading: const CircleAvatar(child: Icon(Icons.receipt_long)),
        title: Text('Quotation ${_shortId(quotation.id)}'),
        subtitle: Text(
          'Total: LKR ${quotation.total.toStringAsFixed(2)}\n'
          'Budget: LKR ${quotation.budget.toStringAsFixed(2)}\n'
          'Items: ${quotation.items.length}',
        ),
        isThreeLine: true,
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            Chip(label: Text(quotation.status)),
            const SizedBox(width: 4),
            if (quotation.isWithinBudget)
              const Icon(Icons.check_circle, color: Colors.green, size: 16)
            else
              const Icon(Icons.warning, color: Colors.orange, size: 16),
          ],
        ),
        onTap: () {
          Navigator.of(context).pushNamed('/quotation', arguments: quotation.id);
        },
      ),
    );
  }

  String _shortId(String id) => id.length > 8 ? '${id.substring(0, 8)}...' : id;
}
