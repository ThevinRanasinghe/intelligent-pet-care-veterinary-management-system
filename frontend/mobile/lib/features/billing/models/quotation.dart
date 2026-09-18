class QuotationItem {
  final String id;
  final String category;
  final String description;
  final int quantity;
  final double unitPrice;
  final double totalPrice;

  QuotationItem({
    required this.id,
    required this.category,
    required this.description,
    required this.quantity,
    required this.unitPrice,
    required this.totalPrice,
  });

  factory QuotationItem.fromJson(Map<String, dynamic> json) {
    return QuotationItem(
      id: json['id'] as String,
      category: json['category'] as String,
      description: json['description'] as String,
      quantity: (json['quantity'] as num).toInt(),
      unitPrice: (json['unitPrice'] as num).toDouble(),
      totalPrice: (json['totalPrice'] as num).toDouble(),
    );
  }
}

class Quotation {
  final String id;
  final String appointmentId;
  final double budget;
  final double subtotal;
  final double total;
  final bool isWithinBudget;
  final String status;
  final List<QuotationItem> items;
  final String createdAt;
  final String updatedAt;

  Quotation({
    required this.id,
    required this.appointmentId,
    required this.budget,
    required this.subtotal,
    required this.total,
    required this.isWithinBudget,
    required this.status,
    required this.items,
    required this.createdAt,
    required this.updatedAt,
  });

  factory Quotation.fromJson(Map<String, dynamic> json) {
    return Quotation(
      id: json['id'] as String,
      appointmentId: json['appointmentId'] as String,
      budget: (json['budget'] as num).toDouble(),
      subtotal: (json['subtotal'] as num).toDouble(),
      total: (json['total'] as num).toDouble(),
      isWithinBudget: json['isWithinBudget'] as bool,
      status: json['status'] as String,
      items: (json['items'] as List<dynamic>)
          .map((e) => QuotationItem.fromJson(e as Map<String, dynamic>))
          .toList(),
      createdAt: json['createdAt'] as String,
      updatedAt: json['updatedAt'] as String,
    );
  }
}
