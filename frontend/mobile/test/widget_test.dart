import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/features/auth/login_page.dart';
import 'package:petcare_mobile/features/home/splash_page.dart';
import 'package:petcare_mobile/main.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('app shows branded splash then lands on LoginPage',
      (WidgetTester tester) async {
    FlutterSecureStorage.setMockInitialValues({});
    await tester.pumpWidget(PetCareApp(
      apiClient: ApiClient(),
      authService: AuthService(ApiClient(), TokenStorage()),
    ));

    // Branded Beacon splash while the session is being restored.
    expect(find.byType(SplashPage), findsOneWidget);
    expect(find.text('BEACON PET HEALTH'), findsOneWidget);

    // No saved session -> LoginPage.
    await tester.pumpAndSettle();
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.text('Sign In'), findsOneWidget);
  });
}
