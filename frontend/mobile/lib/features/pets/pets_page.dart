import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/widgets/app_states.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/pet_card.dart';
import 'pet_provider.dart';
import 'pet_detail_page.dart';
import 'pet_form_page.dart';

/// Owner Pets tab: list of the owner's pets (GET /pets is owner-scoped
/// for PetOwner tokens) with an Add Pet action.
class PetsPage extends StatefulWidget {
  const PetsPage({super.key});

  @override
  State<PetsPage> createState() => _PetsPageState();
}

class _PetsPageState extends State<PetsPage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<PetProvider>().loadMyPets();
    });
  }

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<PetProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('My Pets')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => const PetFormPage()),
        ),
        icon: const Icon(Icons.add),
        label: const Text('Add Pet'),
      ),
      body: RefreshIndicator(
        onRefresh: provider.loadMyPets,
        child: _buildBody(provider),
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
        if (provider.pets.isEmpty) {
          return ListView(
            children: const [
              AppEmptyState(
                message: 'No pets registered yet — tap Add Pet',
                icon: Icons.pets,
                padding: EdgeInsets.only(top: 200),
              ),
            ],
          );
        }
        return ListView.builder(
          itemCount: provider.pets.length,
          itemBuilder: (context, index) => FadeSlideIn(
            delayIndex: index,
            child: PetCard(
              pet: provider.pets[index],
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(
                  builder: (_) => PetDetailPage(pet: provider.pets[index]),
                ),
              ),
            ),
          ),
        );
    }
  }
}
