import 'package:flutter/material.dart';
import 'package:petcare_mobile/features/auth/login_page.dart';
import 'package:petcare_mobile/features/home/home_page.dart';
import 'package:petcare_mobile/features/scheduling/appointment_detail_page.dart';
import 'package:petcare_mobile/features/billing/quotation_detail_page.dart';
import 'package:petcare_mobile/features/approval/approval_detail_page.dart';
import 'package:petcare_mobile/features/approval/approval_history_page.dart';

class AppRouter {
  static Route<dynamic> onGenerateRoute(RouteSettings settings) {
    switch (settings.name) {
      case '/login':
        return MaterialPageRoute(builder: (_) => const LoginPage());
      case '/home':
        return MaterialPageRoute(builder: (_) => const HomePage());
      case '/slot-appointment':
        final id = settings.arguments as String;
        return MaterialPageRoute(builder: (_) => AppointmentDetailPage(appointmentId: id, forSlot: true));
      case '/appointment':
        final id = settings.arguments as String;
        return MaterialPageRoute(builder: (_) => AppointmentDetailPage(appointmentId: id));
      case '/quotation':
        final id = settings.arguments as String;
        return MaterialPageRoute(builder: (_) => QuotationDetailPage(quotationId: id));
      case '/approval':
        final id = settings.arguments as String;
        return MaterialPageRoute(builder: (_) => ApprovalDetailPage(approvalId: id));
      case '/approval-history':
        final id = settings.arguments as String;
        return MaterialPageRoute(builder: (_) => ApprovalHistoryPage(approvalId: id));
      default:
        return MaterialPageRoute(builder: (_) => const _NotFoundPage());
    }
  }
}

class _NotFoundPage extends StatelessWidget {
  const _NotFoundPage();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Not Found')),
      body: const Center(child: Text('Page not found')),
    );
  }
}
