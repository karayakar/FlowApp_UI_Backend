

# FlowApp: Drag-and-Drop Workflow Design Platform

[![Watch the Demo Video](https://img.youtube.com/vi/vd8gF7tGDCo/0.jpg)](https://youtu.be/vd8gF7tGDCo)
> 📺 [Click here to watch the demo video](https://youtu.be/vd8gF7tGDCo)

---

## About the Project

**FlowApp** is a modern and modular platform that allows you to visually design workflows (flows/pipelines) using drag-and-drop.  
Users create flows in the frontend interface using ready-made nodes, while the backend executes and manages these flows.

---

## 🛠️ Installation

### 1. Frontend

```bash
cd flow-app/vite-project
npm install
npm run dev
```

### 2. Backend

```bash
cd flow-app/backend
dotnet build
dotnet run
```

UI: http://localhost:5173/

Backend: https://localhost:53750/

---

## Features

### 🚀 General Features

- Visual workflow design with drag-and-drop
- 40+ ready-to-use nodes (data, network, control, AI, trigger, file, etc.)
- Input/output handles for each node
- Edge (connection) mapping and animated, deletable connections
- Save, load, and export flows
- Run flows and monitor live results
- JSON-based snapshot support
- Parallel and branched flow execution
- Advanced logging and error management

---

### 🖥️ Frontend (React + React Flow)

- Modern, responsive interface (React + TypeScript)
- Node catalog: Add nodes via drag-and-drop
- Settings drawer for each node
- Labels and type badges for input/output handles
- Animated dashed edges with delete/settings buttons
- Flow visualization, zoom, pan, grid
- Export/import flows as JSON
- Node and edge mapping support
- Comprehensive node types (see below)

---

### 🖧 Backend (C# .NET)

- Engine to execute flow snapshots
- Asynchronous processing for each node
- Parallel and branched execution (independent nodes run concurrently)
- Edge mapping and input-output chain management
- Merge (combiner) node and custom node support
- Advanced logging and error management
- HTTP API integration with frontend

---

## 🔗 Node Catalog (Total: **40+** nodes)

Nodes are available in the following categories:

### **Data & Transformation**
- JSON Transform
- JSON Merge
- String Concat
- String Template
- Regex Extract
- Math Operation
- Array Filter / Map

### **Network & Integration**
- HTTP Request
- HTTP Listen
- WebSocket Send / In
- Webhook In / Verify
- Email Send
- MQTT Subscribe
- SSE Subscribe
- Queue Consume

### **File & Storage**
- Read File / Write File
- Cache Get / Set

### **Control & Flow**
- If
- Switch
- Loop
- Delay
- Try / Catch

### **AI & NLP**
- OpenAI Chat
- Text-to-Speech (TTS)
- Speech-to-Text (STT)

### **Triggers**
- Manual Trigger
- Cron
- Interval Timer
- On App Start
- File/Directory Watch
- Keyboard Shortcut
- Bluetooth Notify
- Geo Fence
- Battery Level
- Clipboard Change

> **Note:** Each node has input/output handles, type badges, and a settings panel.

---

## 📦 Usage

1. Drag and drop nodes in the frontend interface.
2. Create connections (edges) between nodes.
3. Configure each node’s settings and mappings.
4. Save or export your flow.
5. Send the flow to the backend to execute and monitor results.

---

## 👨‍💻 Contribution & Development

- To add a new node, update `NODE_TYPES_CATALOG` and implement `INodeProcessor` in the backend.
- For UI/UX improvements, edit React and React Flow components.
- Report bugs and suggestions by [opening an issue](https://github.com/karayakar/issues).

---

## 📹 Video

[![Watch the Demo Video](https://img.youtube.com/vi/vd8gF7tGDCo/0.jpg)](https://youtu.be/vd8gF7tGDCo)
> 📺 [Click here to watch the demo video](https://youtu.be/vd8gF7tGDCo)

---

**Design, manage, and automate your workflows easily with FlowApp!**
