import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/motion/app_motion.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/pet_card.dart';
import '../../core/widgets/segmented_control.dart';
import '../../core/widgets/top_bar.dart';
import 'pet_provider.dart';
import 'pet_detail_page.dart';
import 'pet_form_sheet.dart';
import 'models/pet.dart';

/// Owner Pets tab: two-column grid of the owner's pets (GET /pets is
/// owner-scoped for PetOwner tokens) with a dark circular Add Pet FAB.
/// The form is a bottom sheet, per the Beacon mobile spec.
class PetsPage extends StatefulWidget {
  const PetsPage({super.key});

  @override
  State<PetsPage> createState() => _PetsPageState();
}

class _PetsPageState extends State<PetsPage> {
  /// 0 = Active, 1 = Archived.
  int _segment = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<PetProvider>().loadMyPets();
    });
  }

  Future<void> _openPetForm({Pet? pet}) async {
    await showPetFormSheet(context, pet: pet);
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<PetProvider>();

    return Scaffold(
      appBar: const TopBar(title: 'My Pets', showBackButton: false),
      floatingActionButton: FloatingActionButton(
        onPressed: () => _openPetForm(),
        tooltip: 'Add Pet',
        child: const Icon(Icons.add, size: 28),
      ),
      body: RefreshIndicator(
        onRefresh: provider.loadMyPets,
        // Loading → content crossfades instead of snapping.
        child: AnimatedSwitcher(
          duration: AppMotion.standard,
          child: KeyedSubtree(
            key: ValueKey(provider.listState),
            child: _buildBody(provider),
          ),
        ),
      ),
    );
  }

  Widget _buildBody(PetProvider provider) {
    switch (provider.listState) {
      case LoadState.idle:
      case LoadState.loading:
        return const AppLoading();
      case LoadState.error:
        return AppErrorState(
          message: provider.errorMessage,
          onRetry: provider.loadMyPets,
        );
      case LoadState.success:
        final showArchived = _segment == 1;
        final pets = showArchived ? provider.archivedPets : provider.pets;
        return Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
                  AppSpacing.sm, AppSpacing.pageHorizontal, AppSpacing.xs),
              child: AppSegmentedControl(
                labels: const ['Active', 'Archived'],
                selectedIndex: _segment,
                onSelected: (index) => setState(() => _segment = index),
              ),
            ),
            Expanded(
              child: pets.isEmpty
                  ? _buildEmpty(showArchived)
                  : _buildGrid(pets),
            ),
          ],
        );
    }
  }

  Widget _buildEmpty(bool archived) {
    if (archived) {
      return ListView(
        children: const [
          AppEmptyState(
            message: 'No archived pets',
            hint: 'Removed pets appear here with their history intact.',
            icon: Icons.archive_outlined,
            padding: EdgeInsets.only(top: 160),
          ),
        ],
      );
    }
    return ListView(
      children: [
        AppEmptyState(
          message: 'No pets registered yet',
          hint: 'Add your first pet to start booking visits.',
          icon: Icons.pets,
          padding: const EdgeInsets.only(top: 160),
          actionLabel: 'Add Pet',
          onAction: () => _openPetForm(),
        ),
      ],
    );
  }

  Widget _buildGrid(List<Pet> pets) {
    return GridView.builder(
      padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
          AppSpacing.sm, AppSpacing.pageHorizontal, 96),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: AppSpacing.sm,
        crossAxisSpacing: AppSpacing.sm,
        childAspectRatio: 0.82,
      ),
      itemCount: pets.length,
      itemBuilder: (context, index) {
        final pet = pets[index];
        return FadeSlideIn(
          // 60 ms steps, capped at 5 so large pet lists stay snappy.
          delay: Duration(milliseconds: 60 * index.clamp(0, 5)),
          child: PetCard(
            pet: pet,
            grid: true,
            onTap: () => Navigator.of(context).push(
              MotionPageRoute(page: PetDetailPage(pet: pet)),
            ),
          ),
        );
      },
    );
  }
}
