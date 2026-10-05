import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/theme/app_text_styles.dart';
import '../../core/widgets/app_button.dart';
import '../../core/widgets/app_text_field.dart';
import '../../core/widgets/brand_logo.dart';
import '../../core/widgets/pop_in.dart';
import '../../core/widgets/top_bar.dart';
import 'auth_provider.dart';

/// PetOwner self-registration → POST /auth/register/pet-owner.
/// Fields follow RegisterPetOwnerRequest (firstName/lastName/email/
/// password/confirmPassword). On success the user returns to Sign In.
class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final _formKey = GlobalKey<FormState>();
  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _obscure = true;
  bool _submitting = false;

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    final auth = context.read<AuthProvider>();
    final success = await auth.register(
      firstName: _firstNameController.text.trim(),
      lastName: _lastNameController.text.trim(),
      email: _emailController.text.trim(),
      password: _passwordController.text,
      confirmPassword: _confirmController.text,
    );
    if (!mounted) return;
    setState(() => _submitting = false);
    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Account created — please sign in.')),
      );
      Navigator.of(context).pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(auth.errorMessage ?? 'Registration failed'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    // Beacon auth layout: cream page, pop-in logo tile, hero copy,
    // labelled inputs, full-width dark CTA.
    return Scaffold(
      appBar: const TopBar(title: 'Create Account'),
      body: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(
            horizontal: AppSpacing.pageHorizontal, vertical: 16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Center(
                child: PopIn(child: BrandLogoTile(size: 56, light: true)),
              ),
              const SizedBox(height: 16),
              const Text(
                'JOIN BEACON PET HEALTH',
                textAlign: TextAlign.center,
                style: AppTextStyles.eyebrow,
              ),
              const SizedBox(height: 4),
              const Text(
                'Register as a Pet Owner',
                textAlign: TextAlign.center,
                style: AppTextStyles.pageTitle,
              ),
              const SizedBox(height: 24),
              AppTextField(
                label: 'First Name',
                controller: _firstNameController,
                validator: (v) => v == null || v.trim().isEmpty
                    ? 'First name is required'
                    : null,
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Last Name',
                controller: _lastNameController,
                validator: (v) => v == null || v.trim().isEmpty
                    ? 'Last name is required'
                    : null,
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Email',
                hint: 'you@example.com',
                controller: _emailController,
                keyboardType: TextInputType.emailAddress,
                validator: (v) {
                  if (v == null || v.trim().isEmpty) return 'Email is required';
                  if (!v.contains('@')) return 'Enter a valid email address';
                  return null;
                },
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Password',
                hint: 'Min 8 chars: upper, lower, digit, special',
                controller: _passwordController,
                obscureText: _obscure,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'Password is required';
                  if (v.length < 8) {
                    return 'Password must be at least 8 characters';
                  }
                  return null;
                },
                suffixIcon: IconButton(
                  icon:
                      Icon(_obscure ? Icons.visibility : Icons.visibility_off),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Confirm Password',
                controller: _confirmController,
                obscureText: _obscure,
                validator: (v) {
                  if (v == null || v.isEmpty) {
                    return 'Please confirm your password';
                  }
                  if (v != _passwordController.text) {
                    return 'Passwords do not match';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 24),
              AppButton(
                label: 'Create Account',
                loading: _submitting,
                onPressed: _submit,
              ),
              const SizedBox(height: 16),
            ],
          ),
        ),
      ),
    );
  }
}
