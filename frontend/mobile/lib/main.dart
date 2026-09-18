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
import 'features/auth/login_page.dart';
import 'features/home/home_page.dart';
import 'core/routing/app_router.dart';

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
  bool _initialized = false;

  @override
  void initState() {
    super.initState();
    _authProvider = AuthProvider(widget.authService);
    widget.apiClient.onUnauthorized = _authProvider.logout;
    _schedulingProvider = SchedulingProvider(SchedulingService(widget.apiClient));
    _billingProvider = BillingProvider(BillingService(widget.apiClient));
    _approvalProvider = ApprovalProvider(ApprovalService(widget.apiClient));

    _authProvider.init().then((_) {
      if (mounted) setState(() => _initialized = true);
    });
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: _authProvider),
        ChangeNotifierProvider.value(value: _schedulingProvider),
        ChangeNotifierProvider.value(value: _billingProvider),
        ChangeNotifierProvider.value(value: _approvalProvider),
      ],
      child: Consumer<AuthProvider>(
        builder: (context, auth, _) => MaterialApp(
          key: ValueKey(auth.isAuthenticated),
          title: 'PetCare AI',
          theme: ThemeData(
            colorSchemeSeed: Colors.teal,
            useMaterial3: true,
          ),
          onGenerateRoute: AppRouter.onGenerateRoute,
          home: _initialized ? _buildHome() : const Scaffold(body: Center(child: CircularProgressIndicator())),
        ),
      ),
    );
  }

  Widget _buildHome() {
    return Consumer<AuthProvider>(
      builder: (context, auth, _) {
        if (auth.isAuthenticated) {
          return const HomePage();
        }
        return const LoginPage();
      },
    );
  }
}
