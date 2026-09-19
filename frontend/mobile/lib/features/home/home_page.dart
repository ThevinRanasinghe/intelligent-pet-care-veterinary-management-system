import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../auth/auth_provider.dart';
import '../scheduling/appointment_slots_page.dart';
import '../billing/quotations_page.dart';
import '../approval/approvals_page.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  int _currentIndex = 0;

  final _pages = const [
    AppointmentSlotsPage(),
    QuotationsPage(),
    ApprovalsPage(),
  ];

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    return Scaffold(
      appBar: AppBar(
        title: const Text('PetCare AI'),
        actions: [
          PopupMenuButton<String>(
            onSelected: (value) {
              if (value == 'logout') {
                auth.logout();
              }
            },
            itemBuilder: (_) => [
              PopupMenuItem(value: 'logout', child: Text('Logout (${auth.session?.name ?? ''})')),
            ],
          ),
        ],
      ),
      body: _pages[_currentIndex],
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (i) => setState(() => _currentIndex = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.calendar_today), label: 'Slots'),
          NavigationDestination(icon: Icon(Icons.receipt_long), label: 'Billing'),
          NavigationDestination(icon: Icon(Icons.approval), label: 'Approvals'),
        ],
      ),
    );
  }
}
