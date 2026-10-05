import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/motion/app_motion.dart';
import '../../core/network/api_client.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_button.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/app_text_field.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/pop_in.dart';
import '../../core/widgets/section_header.dart';
import '../../core/widgets/top_bar.dart';
import '../auth/auth_provider.dart';
import '../history/medical_history_page.dart';
import 'models/pet_owner_profile.dart';
import 'profile_service.dart';

/// Owner Profile tab: centred hero (yellow initials avatar, name, email,
/// role badge), account details card, and the Beacon menu list (My Pets /
/// Appointments / Medical History / Bills / Change Password /
/// Edit Profile) followed by Logout.
class ProfilePage extends StatefulWidget {
  /// Optional shell tab switcher (0 Home · 1 Pets · 2 Appointments ·
  /// 3 Bills · 4 Profile). When null — e.g. standalone tests — the tab
  /// shortcut rows are hidden.
  final void Function(int index)? onNavigateToTab;

  const ProfilePage({super.key, this.onNavigateToTab});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  PetOwnerProfile? _profile;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final service = ProfileService(context.read<ApiClient>());
      _profile = await service.getMyOwnerProfile();
    } catch (e) {
      _error = e.toString();
    }
    if (mounted) setState(() => _loading = false);
  }

  /// Initials avatar — mirrors the web .avatar/.top-avatar treatment.
  String _initials(String name) {
    final parts =
        name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.substring(0, 1).toUpperCase();
    return (parts.first.substring(0, 1) + parts.last.substring(0, 1))
        .toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final session = auth.session;

    return Scaffold(
      appBar: const TopBar(title: 'Profile', showBackButton: false),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
            AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
        children: [
          // Hero: centred 80px yellow initials avatar, name, email, role.
          // Entrance: card fades in, avatar pops, badge pops a beat later.
          FadeSlideIn(
            child: AppCard.detail(
              child: Column(
                children: [
                  PopIn(
                    beginScale: 0.7,
                    peakScale: 1.06,
                    delay: const Duration(milliseconds: 120),
                    child: CircleAvatar(
                      radius: 40,
                      backgroundColor: AppColors.primary,
                      child: Text(
                        _initials(session?.name ?? ''),
                        style: const TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.w800,
                          color: AppColors.black,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: AppSpacing.sm),
                  FadeSlideIn(
                    delay: const Duration(milliseconds: 180),
                    distance: 6,
                    child: Column(
                      children: [
                        Text(
                          session?.name ?? '',
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.3,
                            color: AppColors.black,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          session?.email ?? '',
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                              fontSize: 12, color: AppColors.muted),
                        ),
                      ],
                    ),
                  ),
                  if (session?.role != null) ...[
                    const SizedBox(height: AppSpacing.xs),
                    PopIn(
                      beginScale: 0.6,
                      peakScale: 1.1,
                      delay: const Duration(milliseconds: 260),
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 10, vertical: 3),
                        decoration: BoxDecoration(
                          color: AppColors.primarySoft,
                          borderRadius: BorderRadius.circular(999),
                          border: Border.all(color: AppColors.selectedBorder),
                        ),
                        child: Text(
                          session!.role == 'PetOwner'
                              ? 'Pet Owner'
                              : session.role,
                          style: const TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            color: AppColors.black,
                          ),
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          const FadeSlideIn(
            delay: Duration(milliseconds: 200),
            distance: 8,
            child: SectionHeader('Account'),
          ),
          const SizedBox(height: 8),
          FadeSlideIn(
            delay: const Duration(milliseconds: 240),
            distance: 10,
            child: AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Name + email already appear in the header card.
                  if (session?.role != null)
                    DetailRow('Role', value: session!.role, labelWidth: 90),
                  if (_loading)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: AppLoading(),
                    )
                  else if (_error != null)
                    Row(
                      children: [
                        Expanded(child: Text('Could not load phone: $_error')),
                        TextButton(
                            onPressed: _load, child: const Text('Retry')),
                      ],
                    )
                  else if (_profile?.phoneNumber != null)
                    DetailRow('Phone',
                        value: _profile!.phoneNumber!, labelWidth: 90),
                  if (_profile?.address != null &&
                      _profile!.address!.isNotEmpty)
                    DetailRow('Address',
                        value: _profile!.address!, labelWidth: 90),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          const FadeSlideIn(
            delay: Duration(milliseconds: 280),
            distance: 8,
            child: SectionHeader('Menu'),
          ),
          const SizedBox(height: 8),
          FadeSlideIn(
            delay: const Duration(milliseconds: 320),
            distance: 10,
            child: AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  if (widget.onNavigateToTab != null) ...[
                    _MenuRow(
                      icon: Icons.pets,
                      label: 'My Pets',
                      onTap: () => widget.onNavigateToTab!(1),
                    ),
                    const Divider(height: 1, indent: 56, color: AppColors.line),
                    _MenuRow(
                      icon: Icons.event_outlined,
                      label: 'Appointments',
                      onTap: () => widget.onNavigateToTab!(2),
                    ),
                    const Divider(height: 1, indent: 56, color: AppColors.line),
                  ],
                  _MenuRow(
                    icon: Icons.medical_information_outlined,
                    label: 'Medical History',
                    onTap: () => Navigator.of(context).push(
                      MotionPageRoute(page: const MedicalHistoryPage()),
                    ),
                  ),
                  if (widget.onNavigateToTab != null) ...[
                    const Divider(height: 1, indent: 56, color: AppColors.line),
                    _MenuRow(
                      icon: Icons.receipt_long,
                      label: 'Bills',
                      onTap: () => widget.onNavigateToTab!(3),
                    ),
                  ],
                  const Divider(height: 1, indent: 56, color: AppColors.line),
                  _MenuRow(
                    icon: Icons.lock_outline,
                    label: 'Change Password',
                    onTap: () => Navigator.of(context).push(
                      MotionPageRoute(page: const ChangePasswordPage()),
                    ),
                  ),
                  const Divider(height: 1, indent: 56, color: AppColors.line),
                  _MenuRow(
                    icon: Icons.edit_outlined,
                    label: 'Edit Profile',
                    onTap: () async {
                      final updated = await Navigator.of(context).push<bool>(
                        MotionPageRoute(page: const EditProfilePage()),
                      );
                      if (updated == true) _load();
                    },
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 24),
          FadeSlideIn(
            delay: const Duration(milliseconds: 360),
            distance: 8,
            child: AppButton(
              label: 'Logout',
              icon: Icons.logout,
              onPressed: () => context.read<AuthProvider>().logout(),
            ),
          ),
        ],
      ),
    );
  }
}

/// Menu row inside the profile card: icon tile + label + chevron.
class _MenuRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _MenuRow(
      {required this.icon, required this.label, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: ListIconTile(icon: icon),
      title: Text(label, style: const TextStyle(fontSize: 14)),
      trailing: const Icon(Icons.chevron_right, color: AppColors.caption),
      onTap: onTap,
    );
  }
}

/// PUT /auth/profile — first name, last name, phone number.
class EditProfilePage extends StatefulWidget {
  const EditProfilePage({super.key});

  @override
  State<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends State<EditProfilePage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _firstNameController;
  late final TextEditingController _lastNameController;
  late final TextEditingController _phoneController;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    final name = context.read<AuthProvider>().session?.name ?? '';
    final parts = name.trim().split(RegExp(r'\s+'));
    _firstNameController =
        TextEditingController(text: parts.isNotEmpty ? parts.first : '');
    _lastNameController = TextEditingController(
        text: parts.length > 1 ? parts.sublist(1).join(' ') : '');
    _phoneController = TextEditingController();
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    final auth = context.read<AuthProvider>();
    final success = await auth.updateProfile(
      firstName: _firstNameController.text.trim(),
      lastName: _lastNameController.text.trim(),
      phoneNumber: _phoneController.text.trim().isEmpty
          ? null
          : _phoneController.text.trim(),
    );
    if (!mounted) return;
    setState(() => _submitting = false);
    if (success) {
      Navigator.of(context).pop(true);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(auth.errorMessage ?? 'Update failed'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: const TopBar(title: 'Edit Profile'),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
            AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppTextField(
                label: 'First Name *',
                controller: _firstNameController,
                validator: (v) => v == null || v.trim().isEmpty
                    ? 'First name is required'
                    : null,
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Last Name *',
                controller: _lastNameController,
                validator: (v) => v == null || v.trim().isEmpty
                    ? 'Last name is required'
                    : null,
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Phone Number',
                controller: _phoneController,
                keyboardType: TextInputType.phone,
              ),
              const SizedBox(height: 24),
              AppButton(
                label: 'Save',
                loading: _submitting,
                onPressed: _submit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// PUT /auth/change-password — current + new + confirm.
class ChangePasswordPage extends StatefulWidget {
  const ChangePasswordPage({super.key});

  @override
  State<ChangePasswordPage> createState() => _ChangePasswordPageState();
}

class _ChangePasswordPageState extends State<ChangePasswordPage> {
  final _formKey = GlobalKey<FormState>();
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _submitting = false;

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    final auth = context.read<AuthProvider>();
    final success = await auth.changePassword(
      currentPassword: _currentController.text,
      newPassword: _newController.text,
      confirmNewPassword: _confirmController.text,
    );
    if (!mounted) return;
    setState(() => _submitting = false);
    if (success) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Password changed')),
      );
      Navigator.of(context).pop();
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(auth.errorMessage ?? 'Password change failed'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: const TopBar(title: 'Change Password'),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
            AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AppTextField(
                label: 'Current Password *',
                controller: _currentController,
                obscureText: true,
                validator: (v) => v == null || v.isEmpty
                    ? 'Current password is required'
                    : null,
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'New Password *',
                controller: _newController,
                obscureText: true,
                validator: (v) {
                  if (v == null || v.isEmpty) return 'New password is required';
                  if (v.length < 8) {
                    return 'Password must be at least 8 characters';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 16),
              AppTextField(
                label: 'Confirm New Password *',
                controller: _confirmController,
                obscureText: true,
                validator: (v) {
                  if (v == null || v.isEmpty) {
                    return 'Please confirm the password';
                  }
                  if (v != _newController.text) return 'Passwords do not match';
                  return null;
                },
              ),
              const SizedBox(height: 24),
              AppButton(
                label: 'Change Password',
                loading: _submitting,
                onPressed: _submit,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
