import 'package:flutter/material.dart';
import 'database/app_database.dart';

void main() {
  runApp(OsaHealthApp(db: openDatabase()));
}

class OsaHealthApp extends StatelessWidget {
  final AppDatabase db;

  const OsaHealthApp({super.key, required this.db});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'osaHealth — Drift Demo',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.teal),
        useMaterial3: true,
      ),
      home: NotesPage(db: db),
    );
  }
}

class NotesPage extends StatefulWidget {
  final AppDatabase db;

  const NotesPage({super.key, required this.db});

  @override
  State<NotesPage> createState() => _NotesPageState();
}

class _NotesPageState extends State<NotesPage> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _addNote() async {
    final text = _controller.text.trim();
    if (text.isEmpty) return;
    await widget.db.addNote(text);
    _controller.clear();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: Theme.of(context).colorScheme.inversePrimary,
        title: const Text('Drift + SQLite demo'),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(
                  child: TextField(
                    controller: _controller,
                    decoration: const InputDecoration(
                      hintText: 'Enter a note…',
                      border: OutlineInputBorder(),
                    ),
                    onSubmitted: (_) => _addNote(),
                  ),
                ),
                const SizedBox(width: 12),
                FilledButton(
                  onPressed: _addNote,
                  child: const Text('Save'),
                ),
              ],
            ),
          ),
          Expanded(
            child: StreamBuilder<List<Note>>(
              stream: widget.db.watchAllNotes(),
              builder: (context, snapshot) {
                if (snapshot.connectionState == ConnectionState.waiting) {
                  return const Center(child: CircularProgressIndicator());
                }
                final notes = snapshot.data ?? [];
                if (notes.isEmpty) {
                  return const Center(
                    child: Text(
                      'No notes yet — add one above.',
                      style: TextStyle(color: Colors.grey),
                    ),
                  );
                }
                return ListView.builder(
                  itemCount: notes.length,
                  itemBuilder: (context, index) {
                    final note = notes[index];
                    return ListTile(
                      title: Text(note.content),
                      subtitle: Text(
                        _formatDate(note.createdAt),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                      trailing: IconButton(
                        icon: const Icon(Icons.delete_outline),
                        tooltip: 'Delete',
                        onPressed: () => widget.db.deleteNote(note.id),
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  String _formatDate(DateTime dt) {
    return '${dt.year}-${_pad(dt.month)}-${_pad(dt.day)} '
        '${_pad(dt.hour)}:${_pad(dt.minute)}';
  }

  String _pad(int n) => n.toString().padLeft(2, '0');
}
