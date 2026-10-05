import 'package:flutter/material.dart';
import '../../core/motion/app_motion.dart';
import '../../core/widgets/app_bottom_nav.dart';
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
      MotionPageRoute(page: MainShell(initialIndex: index)),
      (_) => false,
    );
  }

  @override
  State<MainShell> createState() => _MainShellState();
}

class _MainShellState extends State<MainShell> {
  late final PageController _pageController =
      PageController(initialPage: widget.initialIndex);
  late int _currentIndex = widget.initialIndex;

  /// Tab taps drive a horizontal carousel: the outgoing page slides out
  /// and the incoming page slides in simultaneously — left when moving
  /// to a higher-index tab, right when moving to a lower-index tab.
  void _goToTab(int index) {
    if (index == _currentIndex) return;
    if (AppMotion.reduceMotion(context)) {
      _pageController.jumpToPage(index);
      return;
    }
    _pageController.animateToPage(
      index,
      duration: AppMotion.tabSlide,
      curve: Curves.easeInOutCubic,
    );
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final pages = <Widget>[
      OwnerHomeTab(onNavigateToTab: _goToTab),
      const PetsPage(),
      const OwnerAppointmentsPage(),
      // QuotationsPage already renders its own 'My Bills' scaffold for the
      // PetOwner role and loads /quotations/mine.
      const QuotationsPage(),
      ProfilePage(onNavigateToTab: _goToTab),
    ];

    if (_currentIndex >= pages.length) _currentIndex = 0;

    return Scaffold(
      // PageView provides the simultaneous slide-out/slide-in; swipe
      // gestures stay disabled — the bottom nav owns navigation.
      body: PageView(
        controller: _pageController,
        physics: const NeverScrollableScrollPhysics(),
        onPageChanged: (index) => setState(() => _currentIndex = index),
        children: [
          for (final page in pages) _KeepAliveTab(child: page),
        ],
      ),
      // Beacon bottom nav: light translucent bar, yellow capsule on the
      // active item. Fixed — only the page area above it slides.
      bottomNavigationBar: AppBottomNav(
        currentIndex: _currentIndex,
        onSelected: _goToTab,
        items: const [
          AppNavItem(icon: Icons.home_outlined, label: 'Home'),
          AppNavItem(icon: Icons.pets_outlined, label: 'My Pets'),
          AppNavItem(icon: Icons.event_outlined, label: 'Appointments'),
          AppNavItem(icon: Icons.receipt_long_outlined, label: 'Bills'),
          AppNavItem(icon: Icons.person_outline, label: 'Profile'),
        ],
      ),
    );
  }
}

/// Keeps a tab's state alive while it is off-screen (PageView would
/// otherwise dispose non-visible children), matching the previous
/// IndexedStack behaviour.
class _KeepAliveTab extends StatefulWidget {
  final Widget child;

  const _KeepAliveTab({required this.child});

  @override
  State<_KeepAliveTab> createState() => _KeepAliveTabState();
}

class _KeepAliveTabState extends State<_KeepAliveTab>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return widget.child;
  }
}
