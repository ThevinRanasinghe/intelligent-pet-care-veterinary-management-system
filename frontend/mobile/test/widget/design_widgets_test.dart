import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:petcare_mobile/core/widgets/pet_card.dart';
import 'package:petcare_mobile/core/widgets/step_dots.dart';
import 'package:petcare_mobile/core/widgets/status_timeline.dart';
import 'package:petcare_mobile/features/pets/models/pet.dart';

Widget host(Widget child) =>
    MaterialApp(home: Scaffold(body: child));

void main() {
  group('PetCard', () {
    testWidgets('shows name, species·breed, age and the View Profile action',
        (tester) async {
      var tapped = 0;
      await tester.pumpWidget(host(PetCard(
        pet: Pet(
          id: 'p1',
          ownerId: 'o1',
          name: 'Max',
          species: 'Dog',
          breed: 'Golden Retriever',
          dateOfBirth: '2023-01-01',
        ),
        onTap: () => tapped++,
      )));

      expect(find.text('Max'), findsOneWidget);
      expect(find.textContaining('Dog · Golden Retriever'), findsOneWidget);
      expect(find.textContaining('years old'), findsOneWidget);
      expect(find.text('View Profile'), findsOneWidget);
      await tester.tap(find.text('View Profile'));
      expect(tapped, 1);
    });

    testWidgets('falls back to the species emoji without a photoUrl',
        (tester) async {
      await tester.pumpWidget(host(PetCard(
        pet: Pet(id: 'p1', ownerId: 'o1', name: 'Kitty', species: 'Cat'),
      )));
      expect(find.text('🐱'), findsOneWidget);
    });
  });

  group('StepDots', () {
    testWidgets('marks done steps with a check and pending with numbers',
        (tester) async {
      await tester.pumpWidget(host(const StepDots(
        currentStep: 2,
        labels: ['Pet', 'Clinic', 'Date', 'Time', 'Details'],
      )));

      expect(find.text('Pet'), findsOneWidget);
      expect(find.text('Details'), findsOneWidget);
      // Steps 1-2 done → two check icons.
      expect(find.byIcon(Icons.check), findsNWidgets(2));
      // Pending steps show their numbers (4 and 5).
      expect(find.text('4'), findsOneWidget);
      expect(find.text('5'), findsOneWidget);
    });
  });

  group('StatusTimeline', () {
    testWidgets('renders done, current and pending steps', (tester) async {
      await tester.pumpWidget(host(const StatusTimeline(
        steps: ['Submitted', 'Assigned', 'Upcoming', 'Done'],
        currentIndex: 2,
      )));

      expect(find.text('Submitted'), findsOneWidget);
      expect(find.text('Done'), findsOneWidget);
      expect(find.byIcon(Icons.check_circle), findsNWidgets(2));
      expect(find.byIcon(Icons.radio_button_checked), findsOneWidget);
      expect(find.byIcon(Icons.circle_outlined), findsNWidgets(1));
    });
  });
}
