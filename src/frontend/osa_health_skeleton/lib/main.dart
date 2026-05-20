import 'package:flutter/material.dart';

void main() {
  runApp(const OsaHealthApp());
}

class OsaHealthApp extends StatelessWidget {
  const OsaHealthApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'osaHealth',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.teal),
        useMaterial3: true,
      ),
      home: const HomePage(),
    );
  }
}

class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('osaHealth'),
      ),
      body: const Center(
        child: Text('Hello, osaHealth!'),
      ),
    );
  }
}
