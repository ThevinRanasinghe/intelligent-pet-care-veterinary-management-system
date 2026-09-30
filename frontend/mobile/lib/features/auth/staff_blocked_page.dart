import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/brand_logo.dart';
import 'auth_provider.dart';

/// Shown when a non-PetOwner account signs in (or a staff session is
/// restored): the mobile app is PetOwner-only — clinic staff use the
/// React web application.
class StaffBlockedPage extends StatelessWidget {
  const StaffBlockedPage({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const BrandLogoTile(size: 64),
              const SizedBox(height: 20),
              const Icon(Icons.lock_outline, size: 40, color: AppColors.black),
              const SizedBox(height: 16),
              const Text(
                'This app is for Pet Owners',
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w800,
                  letterSpacing: -0.4,
                  color: AppColors.black,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                'This app is for Pet Owners — staff should use the web application.'
                '${auth.session?.role != null ? '\n\nSigned in as: ${auth.session!.role}' : ''}',
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 13, color: AppColors.muted),
              ),
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: () => context.read<AuthProvider>().logout(),
                icon: const Icon(Icons.logout),
                label: const Text('Logout'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
