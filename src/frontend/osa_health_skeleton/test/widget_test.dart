import 'package:flutter_test/flutter_test.dart';
import 'package:osa_health_skeleton/main.dart';

void main() {
  testWidgets('renders hello world', (WidgetTester tester) async {
    await tester.pumpWidget(const OsaHealthApp());
    expect(find.text('Hello, osaHealth!'), findsOneWidget);
  });
}
