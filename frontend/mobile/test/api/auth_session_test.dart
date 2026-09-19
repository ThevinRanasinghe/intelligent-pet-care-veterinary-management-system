import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:petcare_mobile/core/auth/auth_service.dart';
import 'package:petcare_mobile/core/auth/token_storage.dart';
import 'package:petcare_mobile/core/network/api_client.dart';
import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/features/auth/auth_provider.dart';
import 'package:petcare_mobile/features/auth/login_page.dart';
import 'package:petcare_mobile/features/billing/billing_provider.dart';
import 'package:petcare_mobile/features/billing/billing_service.dart';
import 'package:petcare_mobile/main.dart';
import 'package:provider/provider.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  Map<String, dynamic> sessionJson() => {
    'token': 'test-token',
    'expiresAt': DateTime.now().add(const Duration(hours: 1)).toIso8601String(),
    'userId': 'user-1', 'email': 'test@example.test', 'name': 'Test User',
    'role': 'ClinicManager',
  };

  Future<AuthService> signIn(ApiClient client, TokenStorage storage) async {
    final service = AuthService(client, storage);
    await service.login('test@example.test', 'test-password');
    return service;
  }

  test('login stores session and attaches exact Bearer header to API requests', () async {
    final transport = MockClient((request) async {
      if (request.url.path.endsWith('/auth/login')) {
        expect(request.method, 'POST');
        expect(jsonDecode(request.body), {'email': 'test@example.test', 'password': 'test-password'});
        return http.Response(jsonEncode(sessionJson()), 200);
      }
      expect(request.headers['Authorization'], 'Bearer test-token');
      return http.Response('[]', 200);
    });
    addTearDown(transport.close);
    final client = ApiClient(client: transport);
    final storage = TokenStorage();
    await signIn(client, storage);
    expect((await storage.getSession())?.userId, 'user-1');
    await client.get<dynamic>('/quotations');
  });

  test('restoration loads valid saved session and logout clears storage and token', () async {
    final transport = MockClient((_) async => http.Response(jsonEncode(sessionJson()), 200));
    addTearDown(transport.close);
    final storage = TokenStorage();
    await signIn(ApiClient(client: transport), storage);
    final client = ApiClient(client: transport);
    final provider = AuthProvider(AuthService(client, storage));
    addTearDown(provider.dispose);
    await provider.init();
    expect(provider.isAuthenticated, isTrue);
    expect(client.token, 'test-token');
    await provider.logout();
    expect(provider.session, isNull);
    expect(provider.isAuthenticated, isFalse);
    expect(client.token, isNull);
    expect(await storage.getToken(), isNull);
  });

  test('expired session is not restored', () async {
    final storage = TokenStorage();
    await storage.save(token: 'expired', expiresAt: '2000-01-01T00:00:00Z',
      userId: 'user-1', email: 'test@example.test', name: 'Test', role: 'ClinicManager');
    final transport = MockClient((_) async => http.Response('', 500));
    addTearDown(transport.close);
    final client = ApiClient(client: transport);
    final service = AuthService(client, storage);
    expect(await service.restoreSession(), isNull);
    expect(client.token, isNull);
  });

  test('invalid login reports API message without creating a session', () async {
    final transport = MockClient((_) async => http.Response('{"detail":"Invalid credentials"}', 401));
    addTearDown(transport.close);
    final storage = TokenStorage();
    final provider = AuthProvider(AuthService(ApiClient(client: transport), storage));
    addTearDown(provider.dispose);
    expect(await provider.login('test@example.test', 'incorrect'), isFalse);
    expect(provider.errorMessage, 'Invalid credentials');
    expect(provider.isAuthenticated, isFalse);
    expect(await storage.getToken(), isNull);
  });

  test('401 clears in-memory token even without callback', () async {
    final transport = MockClient((_) async => http.Response('', 401));
    addTearDown(transport.close);
    final client = ApiClient(client: transport)..setToken('expired');
    await expectLater(client.get<dynamic>('/quotations'), throwsA(isA<ApiError>()));
    expect(client.token, isNull);
  });

  test('403 preserves token and presents access denied', () async {
    final transport = MockClient((_) async => http.Response('', 403));
    addTearDown(transport.close);
    var unauthorizedCalls = 0;
    final client = ApiClient(client: transport, onUnauthorized: () { unauthorizedCalls++; })..setToken('valid');
    final provider = BillingProvider(BillingService(client));
    addTearDown(provider.dispose);
    await provider.loadQuotations();
    expect(provider.listState, LoadState.error);
    expect(provider.errorMessage, contains('Access denied'));
    expect(client.token, 'valid');
    expect(unauthorizedCalls, 0);
  });

  testWidgets('login logout and login again switch screens without stale routes', (tester) async {
    final transport = MockClient((request) async => request.url.path.endsWith('/auth/login')
        ? http.Response(jsonEncode(sessionJson()), 200)
        : http.Response('[]', 200));
    addTearDown(transport.close);
    final client = ApiClient(client: transport);
    final storage = TokenStorage();
    await tester.pumpWidget(PetCareApp(apiClient: client, authService: AuthService(client, storage)));
    await tester.pumpAndSettle();
    for (var attempt = 0; attempt < 2; attempt++) {
      expect(find.byType(LoginPage), findsOneWidget);
      await tester.enterText(find.byType(TextFormField).at(0), 'test@example.test');
      await tester.enterText(find.byType(TextFormField).at(1), 'test-password');
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();
      expect(find.byType(NavigationBar), findsOneWidget);
      expect(client.token, 'test-token');
      await tester.tap(find.byType(PopupMenuButton<String>));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Logout (Test User)'));
      await tester.pumpAndSettle();
      expect(find.byType(LoginPage), findsOneWidget);
      expect(client.token, isNull);
      expect(await storage.getToken(), isNull);
    }
  });

  for (final status in [401, 403]) {
    testWidgets('$status updates app session and handles a pushed route correctly', (tester) async {
      var responseStatus = 200;
      final transport = MockClient((request) async {
        if (request.url.path.endsWith('/auth/login')) {
          return http.Response(jsonEncode(sessionJson()), 200);
        }
        return http.Response(responseStatus == 200 ? '[]' : '', responseStatus);
      });
      addTearDown(transport.close);
      final client = ApiClient(client: transport);
      final storage = TokenStorage();
      final service = await signIn(client, storage);
      await tester.pumpWidget(PetCareApp(apiClient: client, authService: service));
      await tester.pumpAndSettle();
      final context = tester.element(find.byType(NavigationBar));
      final auth = context.read<AuthProvider>();
      Navigator.of(context).push(MaterialPageRoute<void>(
        builder: (_) => const Scaffold(body: Text('Protected detail')),
      ));
      await tester.pumpAndSettle();
      responseStatus = status;
      await expectLater(client.get<dynamic>('/quotations'), throwsA(isA<ApiError>()));
      await tester.pumpAndSettle();
      if (status == 401) {
        expect(auth.isAuthenticated, isFalse);
        expect(auth.session, isNull);
        expect(client.token, isNull);
        expect(await storage.getToken(), isNull);
        expect(find.byType(LoginPage), findsOneWidget);
        expect(find.text('Protected detail'), findsNothing);
      } else {
        expect(auth.isAuthenticated, isTrue);
        expect(client.token, 'test-token');
        expect(await storage.getToken(), 'test-token');
        expect(find.text('Protected detail'), findsOneWidget);
        expect(find.byType(LoginPage), findsNothing);
      }
    });
  }
}
