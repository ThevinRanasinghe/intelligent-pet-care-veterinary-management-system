import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'core/network/api_client.dart';
import 'core/auth/token_storage.dart';
import 'core/auth/auth_service.dart';
import 'features/auth/auth_provider.dart';
import 'features/scheduling/scheduling_service.dart';
import 'features/scheduling/scheduling_provider.dart';
import 'features/billing/billing_service.dart';
import 'features/billing/billing_provider.dart';
import 'features/approval/approval_service.dart';
import 'features/approval/approval_provider.dart';
import 'features/pets/pet_service.dart';
import 'features/pets/pet_provider.dart';
import 'features/consultations/consultation_service.dart';
import 'features/consultations/consultation_provider.dart';
import 'features/history/history_service.dart';
import 'features/history/history_provider.dart';
import 'features/auth/login_page.dart';
import 'features/auth/staff_blocked_page.dart';
import 'features/home/main_shell.dart';
import 'features/home/splash_page.dart';
import 'core/motion/app_motion.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final apiClient = ApiClient();
  final tokenStorage = TokenStorage();
  final authService = AuthService(apiClient, tokenStorage);

  runApp(PetCareApp(
    apiClient: apiClient,
    authService: authService,
  ));
}

/// PetOwner-only application: the branded splash covers session restore;
/// afterwards a PetOwner session lands on [MainShell], a staff session on
/// [StaffBlockedPage], and no session on [LoginPage].
class PetCareApp extends StatefulWidget {
  final ApiClient apiClient;
  final AuthService authService;

  const PetCareApp({
    super.key,
    required this.apiClient,
    required this.authService,
  });

  @override
  State<PetCareApp> createState() => _PetCareAppState();
}

class _PetCareAppState extends State<PetCareApp> {
  late final AuthProvider _authProvider;
  late final SchedulingProvider _schedulingProvider;
  late final BillingProvider _billingProvider;
  late final ApprovalProvider _approvalProvider;
  late final PetProvider _petProvider;
  late final ConsultationProvider _consultationProvider;
  late final HistoryProvider _historyProvider;
  bool _initialized = false;

  @override
  void initState() {
    super.initState();
    _authProvider = AuthProvider(widget.authService);
    widget.apiClient.onUnauthorized = _authProvider.logout;
    _schedulingProvider =
        SchedulingProvider(SchedulingService(widget.apiClient));
    _billingProvider = BillingProvider(BillingService(widget.apiClient));
    _approvalProvider = ApprovalProvider(ApprovalService(widget.apiClient));
    _petProvider = PetProvider(PetService(widget.apiClient));
    _consultationProvider =
        ConsultationProvider(ConsultationService(widget.apiClient));
    _historyProvider = HistoryProvider(HistoryService(widget.apiClient));

    _authProvider.init().then((_) {
      if (mounted) setState(() => _initialized = true);
    });
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: widget.apiClient),
        ChangeNotifierProvider.value(value: _authProvider),
        ChangeNotifierProvider.value(value: _schedulingProvider),
        ChangeNotifierProvider.value(value: _billingProvider),
        ChangeNotifierProvider.value(value: _approvalProvider),
        ChangeNotifierProvider.value(value: _petProvider),
        ChangeNotifierProvider.value(value: _consultationProvider),
        ChangeNotifierProvider.value(value: _historyProvider),
      ],
      // Fade-through on auth swaps: when the session flips (login →
      // home, or 401 → login) the MaterialApp is recreated to reset the
      // navigator — the AnimatedSwitcher crossfades the swap so no blank
      // or abrupt frame is exposed.
      child: Consumer<AuthProvider>(
        builder: (context, auth, _) => AnimatedSwitcher(
          duration: AppMotion.standard,
          transitionBuilder: AppMotion.fadeThroughTransition,
          child: MaterialApp(
            key: ValueKey(auth.isAuthenticated),
            title: 'Beacon Pet Health',
            theme: AppTheme.light,
            onGenerateRoute: AppRouter.onGenerateRoute,
            home: AnimatedSwitcher(
              duration: AppMotion.standard,
              transitionBuilder: AppMotion.fadeThroughTransition,
              child: KeyedSubtree(
                key: ValueKey(_initialized),
                child: _initialized ? _buildHome() : const SplashPage(),
              ),
            ),
            // Beacon mobile rail: the app is a mobile layout even on wide
            // screens — content is centred and capped at 448px, never a
            // desktop dashboard.
            builder: (context, child) => Align(
              alignment: Alignment.topCenter,
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 448),
                child: child,
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildHome() {
    return Consumer<AuthProvider>(
      builder: (context, auth, _) {
        if (!auth.isAuthenticated) return const LoginPage();
        if (auth.isPetOwner) return const MainShell();
        // Authenticated but not a PetOwner — staff workflows live on the
        // React web application.
        return const StaffBlockedPage();
      },
    );
  }
}
