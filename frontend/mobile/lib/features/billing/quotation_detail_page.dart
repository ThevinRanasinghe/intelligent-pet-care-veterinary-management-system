import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
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
      appBar: AppBar(title: const Text('Quotation Details')),
      body: _buildBody(provider),
    );
  }

  Widget _buildBody(BillingProvider provider) {
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
                onPressed: () => provider.loadQuotation(widget.quotationId),
                child: const Text('Retry'),
              ),
            ],
          ),
        );
      case LoadState.success:
        final q = provider.quotation;
        if (q == null) {
          return const Center(child: Text('Quotation not found'));
        }
        return ListView(
          padding: const EdgeInsets.all(16),
          children: [
            _row('Quotation ID', q.id),
            _row('Appointment ID', q.appointmentId),
            _row('Status', q.status),
            _row('Budget', 'LKR ${q.budget.toStringAsFixed(2)}'),
            _row('Subtotal', 'LKR ${q.subtotal.toStringAsFixed(2)}'),
            _row('Total', 'LKR ${q.total.toStringAsFixed(2)}'),
            _row('Within Budget', q.isWithinBudget ? 'Yes' : 'No'),
            _row('Created', q.createdAt),
            _row('Updated', q.updatedAt),
            const SizedBox(height: 16),
            const Text('Line Items', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 8),
            ...q.items.map((item) => Card(
                  child: ListTile(
                    title: Text(item.description),
                    subtitle: Text('${item.category}  x${item.quantity}  @ LKR ${item.unitPrice.toStringAsFixed(2)}'),
                    trailing: Text('LKR ${item.totalPrice.toStringAsFixed(2)}'),
                  ),
                )),
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
            width: 130,
            child: Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}
