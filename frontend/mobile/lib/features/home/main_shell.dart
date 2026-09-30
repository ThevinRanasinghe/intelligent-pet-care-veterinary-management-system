import 'package:flutter/material.dart';
import '../billing/quotations_page.dart';
import '../pets/pets_page.dart';
import '../profile/profile_page.dart';
import '../scheduling/owner_appointments_page.dart';
import 'owner_home_tab.dart';

/// Pet-owner shell: the mobile app is PetOwner-only (staff workflows stay
/// on the React web app). Exactly five tabs — Home, My Pets, Appointments,
/// Bills, Profile.
class MainShell extends StatefulWidget {
  final int initialIndex;

  const MainShell({super.key, this.initialIndex = 0});

  /// Replaces the whole navigation stack with the shell opened on [index]
  /// (used by the booking success screen to land on Appointments).
  static void replaceWithTab(BuildContext context, int index) {
    Navigator.of(context, rootNavigator: true).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => MainShell(initialIndex: index)),
      (_) => false,
    );
  }

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  late int _currentIndex = widget.initialIndex;

  void _goToTab(int index) => setState(() => _currentIndex = index);

  @override
  Widget build(BuildContext context) {
    final pages = <Widget>[
      OwnerHomeTab(onNavigateToTab: _goToTab),
      const PetsPage(),
      const OwnerAppointmentsPage(),
      // QuotationsPage already renders its own 'My Bills' scaffold for the
      // PetOwner role and loads /quotations/mine.
      const QuotationsPage(),
      const ProfilePage(),
    ];

    if (_currentIndex >= pages.length) _currentIndex = 0;

    return Scaffold(
      body: IndexedStack(index: _currentIndex, children: pages),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: _goToTab,
        destinations: const [
          NavigationDestination(
              icon: Icon(Icons.home_outlined),
              selectedIcon: Icon(Icons.home),
              label: 'Home'),
          NavigationDestination(
              icon: Icon(Icons.pets_outlined),
              selectedIcon: Icon(Icons.pets),
              label: 'My Pets'),
          NavigationDestination(
              icon: Icon(Icons.event_outlined),
              selectedIcon: Icon(Icons.event),
              label: 'Appointments'),
          NavigationDestination(
              icon: Icon(Icons.receipt_long_outlined),
              selectedIcon: Icon(Icons.receipt_long),
              label: 'Bills'),
          NavigationDestination(
              icon: Icon(Icons.person_outline),
              selectedIcon: Icon(Icons.person),
              label: 'Profile'),
        ],
      ),
    );
  }
}
