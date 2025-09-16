/* eslint-disable react-refresh/only-export-components */
/* eslint-disable @typescript-eslint/no-explicit-any */
// index.tsx
import React, { useCallback, useMemo, useState, useEffect } from "react";
import { createRoot } from "react-dom/client";
import "bootstrap/dist/css/bootstrap.min.css";
import "bootstrap-icons/font/bootstrap-icons.css";
import {
  Button,
  Offcanvas,
  Form,
  Tab,
  Tabs,
  Badge,
  OverlayTrigger,
  Tooltip,
  InputGroup,
  Modal, 
  Spinner,
} from "react-bootstrap";

import Dropdown from "react-bootstrap/Dropdown";
import { create } from "zustand";
import { v4 as uuidv4 } from "uuid";

// XYFlow - named imports (no default)
import {
  BaseEdge,
  EdgeLabelRenderer,
  getBezierPath,
  ReactFlow,
  ReactFlowProvider,
  addEdge,
  applyNodeChanges,
  applyEdgeChanges,
  Background,
  Controls,
  MiniMap,
  Handle,
  Position,
  useReactFlow,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import type { EdgeProps } from "@xyflow/react";

//import JsonThree from "./components/JsonTree";
//import  listJsonPaths from "./utils/jsonPath";

/* -------------------------------- Types & Catalog -------------------------------- */
const API_BASE = (import.meta as any).env?.VITE_API_BASE || "https://localhost:53750";

type IOType = "string" | "number" | "boolean" | "object" | "array" | "any";

const NODE_TYPES_CATALOG = [
  /* ---- Network ---- */
  {
    type: "httpRequest",
    label: "HTTP Request",
    icon: "bi-cloud-arrow-down",
    category: "Network",
    defaults: {
      method: "GET",
      url: "https://api.example.com/users",
      headers: { Authorization: "Bearer {{secrets.API_TOKEN}}" },
      body: null,
    },
    inputs: [],
    outputs: [
      { name: "response", type: "object" },
      { name: "status", type: "number" },
    ],
  },
  {
    type: "websocket",
    label: "WebSocket Send",
    icon: "bi-broadcast",
    category: "Network",
    defaults: { url: "wss://example.com/socket", message: "" },
    inputs: [{ name: "message", type: "string" }],
    outputs: [{ name: "ack", type: "object" }],
  },
  {
    type: "emailSend",
    label: "Send Email",
    icon: "bi-envelope-paper",
    category: "Network",
    defaults: { to: "", subject: "", body: "" },
    inputs: [{ name: "body", type: "string" }],
    outputs: [{ name: "status", type: "number" }],
  },

  /* ---- Data ---- */
  {
    type: "jsonTransform",
    label: "JSON Transform",
    icon: "bi-braces-asterisk",
    category: "Data",
    defaults: { expression: "users[].name" },
    inputs: [{ name: "input", type: "object" }],
    outputs: [{ name: "result", type: "array" }],
  },
  {
    type: "mathOp",
    label: "Math Operation",
    icon: "bi-calculator",
    category: "Data",
    defaults: { expression: "a + b" },
    inputs: [
      { name: "a", type: "number" },
      { name: "b", type: "number" },
    ],
    outputs: [{ name: "result", type: "number" }],
  },
  {
    type: "stringConcat",
    label: "String Concat",
    icon: "bi-type",
    category: "Data",
    defaults: { separator: " " },
    inputs: [
      { name: "left", type: "string" },
      { name: "right", type: "string" },
    ],
    outputs: [{ name: "text", type: "string" }],
  },
  {
    type: "stringTemplate",
    label: "String Template",
    icon: "bi-fonts",
    category: "Data",
    defaults: { template: "Hello {{name}}!" },
    inputs: [{ name: "context", type: "object" }],
    outputs: [{ name: "text", type: "string" }],
  },
  {
    type: "regexExtract",
    label: "Regex Extract",
    icon: "bi-search",
    category: "Data",
    defaults: { pattern: "(\\w+)" },
    inputs: [{ name: "text", type: "string" }],
    outputs: [{ name: "matches", type: "array" }],
  },
  {
    type: "jsonMerge",
    label: "JSON Merge",
    icon: "bi-layers",
    category: "Data",
    defaults: { strategy: "shallow" }, // shallow | deep
    inputs: [
      { name: "left", type: "object" },
      { name: "right", type: "object" },
    ],
    outputs: [{ name: "result", type: "object" }],
  },
  {
    type: "arrayFilter",
    label: "Array Filter",
    icon: "bi-funnel",
    category: "Data",
    defaults: { predicate: "item.active == true" },
    inputs: [{ name: "array", type: "array" }],
    outputs: [{ name: "result", type: "array" }],
  },
  {
    type: "arrayMap",
    label: "Array Map",
    icon: "bi-diagram-3",
    category: "Data",
    defaults: { mapper: "item.name" },
    inputs: [{ name: "array", type: "array" }],
    outputs: [{ name: "result", type: "array" }],
  },

  /* ---- Control ---- */
  {
    type: "if",
    label: "If",
    icon: "bi-diagram-2",
    category: "Control",
    defaults: { condition: "status == 200" },
    inputs: [{ name: "input", type: "any" }],
    outputs: [
      { name: "true", type: "any" },
      { name: "false", type: "any" },
    ],
  },
  {
    type: "loop",
    label: "Loop",
    icon: "bi-arrow-repeat",
    category: "Control",
    defaults: { iterations: 3 },
    inputs: [{ name: "input", type: "any" }],
    outputs: [
      { name: "each", type: "any" },
      { name: "done", type: "any" },
    ],
  },
  {
    type: "delay",
    label: "Delay",
    icon: "bi-hourglass-split",
    category: "Control",
    defaults: { ms: 1000 },
    inputs: [{ name: "input", type: "any" }],
    outputs: [{ name: "output", type: "any" }],
  },
  {
    type: "switch",
    label: "Switch",
    icon: "bi-toggle2-on",
    category: "Control",
    defaults: { cases: ["A", "B"], default: true },
    inputs: [{ name: "value", type: "any" }],
    outputs: [
      { name: "A", type: "any" },
      { name: "B", type: "any" },
      { name: "default", type: "any" },
    ],
  },
  {
    type: "tryCatch",
    label: "Try / Catch",
    icon: "bi-shield-exclamation",
    category: "Control",
    defaults: { },
    inputs: [{ name: "input", type: "any" }],
    outputs: [
      { name: "try", type: "any" },
      { name: "catch", type: "any" },
      { name: "finally", type: "any" },
    ],
  },

  /* ---- AI ---- */
  {
    type: "openaiChat",
    label: "OpenAI Chat",
    icon: "bi-robot",
    category: "AI",
    defaults: { model: "gpt-4o-mini", prompt: "" },
    inputs: [{ name: "prompt", type: "string" }],
    outputs: [{ name: "response", type: "string" }],
  },
  {
    type: "tts",
    label: "Text-to-Speech",
    icon: "bi-soundwave",
    category: "AI",
    defaults: { voice: "tr-TR-ZehraNeural", format: "wav" },
    inputs: [{ name: "text", type: "string" }],
    outputs: [{ name: "audio", type: "object" }],
  },
  {
    type: "stt",
    label: "Speech-to-Text",
    icon: "bi-mic",
    category: "AI",
    defaults: { language: "tr-TR" },
    inputs: [{ name: "audio", type: "object" }],
    outputs: [{ name: "text", type: "string" }],
  },

  /* ---- Storage ---- */
  {
    type: "readFile",
    label: "Read File",
    icon: "bi-file-earmark-text",
    category: "Storage",
    defaults: { path: "/tmp/data.txt", encoding: "utf-8" },
    inputs: [],
    outputs: [{ name: "content", type: "string" }],
  },
  {
    type: "writeFile",
    label: "Write File",
    icon: "bi-save2",
    category: "Storage",
    defaults: { path: "/tmp/data.txt", encoding: "utf-8" },
    inputs: [{ name: "content", type: "string" }],
    outputs: [{ name: "success", type: "boolean" }],
  },

  /* ---- Cache ---- */
  {
    type: "cacheGet",
    label: "Cache Get",
    icon: "bi-box-arrow-in-down",
    category: "Storage",
    defaults: { key: "myKey" },
    inputs: [],
    outputs: [
      { name: "value", type: "any" },
      { name: "found", type: "boolean" },
    ],
  },
  {
    type: "cacheSet",
    label: "Cache Set",
    icon: "bi-box-arrow-up",
    category: "Storage",
    defaults: { key: "myKey", ttlSec: 300 },
    inputs: [{ name: "value", type: "any" }],
    outputs: [{ name: "success", type: "boolean" }],
  },
  /* ---- Triggers ---- */
{
  type: "httpListen",
  label: "HTTP Listen",
  icon: "bi-bullseye",
  category: "Triggers",
  // Not: gerçek dinleme server-side gerekir; burada sadece şema
  defaults: {
    method: "POST",            // GET | POST | PUT | DELETE | ANY
    path: "/api/ingest",       // dinlenecek yol
    auth: "none",              // none | basic | bearer
    port: 8080,                // server port (uygulaman)
    parse: "json"              // json | form | text | raw
  },
  inputs: [],
  outputs: [
    { name: "request", type: "object" },   // { headers, query, params, body }
    { name: "context", type: "object" }    // isteğe bağlı metadata
  ],
},
{
  type: "webhookIn",
  label: "Webhook In",
  icon: "bi-link-45deg",
  category: "Triggers",
  defaults: {
    secret: "",                 // imza doğrulama için (HMAC vs)
    signatureHeader: "X-Signature",
    toleranceSec: 300,
    path: "/webhook/provider"
  },
  inputs: [],
  outputs: [
    { name: "event", type: "object" },
    { name: "raw", type: "object" }
  ],
},
{
  type: "websocketIn",
  label: "WebSocket In",
  icon: "bi-broadcast-pin",
  category: "Triggers",
  defaults: {
    url: "wss://example.com/socket",
    subprotocols: [],
    reconnect: true
  },
  inputs: [],
  outputs: [
    { name: "message", type: "string" },
    { name: "meta", type: "object" } // { type: "open|close|error", ... }
  ],
},
{
  type: "sseSubscribe",
  label: "SSE Subscribe",
  icon: "bi-rss",
  category: "Triggers",
  defaults: {
    url: "https://example.com/events",
    headers: {}
  },
  inputs: [],
  outputs: [
    { name: "event", type: "object" },    // { event, data, id }
    { name: "data", type: "string" }
  ],
},
{
  type: "cron",
  label: "Cron",
  icon: "bi-clock-history",
  category: "Triggers",
  defaults: {
    expression: "*/5 * * * *",  // her 5 dk
    timezone: "Europe/Istanbul"
  },
  inputs: [],
  outputs: [
    { name: "tick", type: "object" } // { now, prev }
  ],
},
{
  type: "intervalTimer",
  label: "Interval Timer",
  icon: "bi-hourglass-split",
  category: "Triggers",
  defaults: {
    ms: 10000,                // 10 sn
    maxFires: 0               // 0 = sınırsız
  },
  inputs: [],
  outputs: [
    { name: "tick", type: "object" }
  ],
},
{
  type: "httpPoller",
  label: "HTTP Poller",
  icon: "bi-arrow-clockwise",
  category: "Triggers",
  defaults: {
    url: "https://api.example.com/updates",
    method: "GET",
    headers: {},
    intervalMs: 15000,
    parse: "json",
    etagSupport: true,
    ifModifiedOnly: true
  },
  inputs: [],
  outputs: [
    { name: "data", type: "any" },
    { name: "status", type: "number" }
  ],
},
{
  type: "fileWatch",
  label: "File Watch",
  icon: "bi-file-earmark-richtext",
  category: "Triggers",
  defaults: {
    path: "/tmp/file.txt",
    events: ["change", "rename"], // platforma göre simülasyon
    debounceMs: 200
  },
  inputs: [],
  outputs: [
    { name: "change", type: "object" } // { event, path }
  ],
},
{
  type: "directoryWatch",
  label: "Directory Watch",
  icon: "bi-folder-symlink",
  category: "Triggers",
  defaults: {
    dir: "/tmp/inbox",
    glob: "*.*",
    includeSubdirs: false,
    debounceMs: 200
  },
  inputs: [],
  outputs: [
    { name: "file", type: "object" } // { event: add|change|unlink, path }
  ],
},
{
  type: "mqttSubscribe",
  label: "MQTT Subscribe",
  icon: "bi-wifi",
  category: "Triggers",
  defaults: {
    brokerUrl: "mqtt://localhost:1883",
    topic: "sensors/#",
    qos: 0,
    username: "",
    password: ""
  },
  inputs: [],
  outputs: [
    { name: "message", type: "string" },
    { name: "topic", type: "string" }
  ],
},
{
  type: "queueConsume",
  label: "Queue Consume",
  icon: "bi-inboxes",
  category: "Triggers",
  defaults: {
    driver: "kafka",  // kafka | rabbitmq | redis
    topic: "events",
    group: "flow-consumer",
    autoAck: true
  },
  inputs: [],
  outputs: [
    { name: "message", type: "object" },
    { name: "meta", type: "object" }
  ],
},
{
  type: "smtpInbound",
  label: "SMTP Inbound",
  icon: "bi-inbox-fill",
  category: "Triggers",
  defaults: {
    host: "imap.example.com",
    port: 993,
    protocol: "imap",    // imap | pop3 (sembolik)
    username: "",
    password: "",
    folder: "INBOX",
    pollSec: 30
  },
  inputs: [],
  outputs: [
    { name: "email", type: "object" }  // { from, to, subject, text, html, attachments }
  ],
},
{
  type: "keyboardShortcut",
  label: "Keyboard Shortcut",
  icon: "bi-keyboard",
  category: "Triggers",
  defaults: {
    combo: "Ctrl+Shift+K",
    global: false        // tarayıcı penceresi içinde
  },
  inputs: [],
  outputs: [
    { name: "pressed", type: "object" } // { combo, time }
  ],
},
{
  type: "manualTrigger",
  label: "Manual Trigger",
  icon: "bi-hand-index",
  category: "Triggers",
  defaults: { note: "Click to fire" },
  inputs: [],
  outputs: [
    { name: "fired", type: "object" }
  ],
},
{
  type: "onStart",
  label: "On App Start",
  icon: "bi-play-circle",
  category: "Triggers",
  defaults: { once: true },
  inputs: [],
  outputs: [
    { name: "boot", type: "object" }
  ],
},
{
  type: "webhookVerify",
  label: "Webhook Verify (HMAC)",
  icon: "bi-shield-lock",
  category: "Triggers",
  defaults: {
    secret: "",
    algorithm: "sha256",
    header: "X-Hub-Signature-256"
  },
  inputs: [],
  outputs: [
    { name: "verified", type: "object" },
    { name: "failed", type: "object" }
  ],
},
{
  type: "bluetoothNotify",
  label: "Bluetooth Notify",
  icon: "bi-bluetooth",
  category: "Triggers",
  defaults: {
    deviceId: "",
    characteristic: ""
  },
  inputs: [],
  outputs: [
    { name: "data", type: "array" } // bytes
  ],
},
{
  type: "geoFence",
  label: "Geo Fence",
  icon: "bi-geo-alt",
  category: "Triggers",
  defaults: {
    center: { lat: 41.0082, lon: 28.9784 },
    radiusM: 500,
    event: "enter" // enter | exit | both
  },
  inputs: [],
  outputs: [
    { name: "location", type: "object" }
  ],
},
{
  type: "batteryLevel",
  label: "Battery Level",
  icon: "bi-battery-half",
  category: "Triggers",
  defaults: {
    threshold: 20,
    direction: "below" // below | above
  },
  inputs: [],
  outputs: [
    { name: "status", type: "object" }
  ],
},
{
  type: "clipboardChange",
  label: "Clipboard Change",
  icon: "bi-clipboard-data",
  category: "Triggers",
  defaults: { pollMs: 1500 },
  inputs: [],
  outputs: [
    { name: "text", type: "string" }
  ],
},

];


const typeColor = (t: IOType) =>
  (
    {
      string: "#3b82f6",
      number: "#10b981",
      boolean: "#f59e0b",
      object: "#8b5cf6",
      array: "#ef4444",
      any: "#6b7280",
    } as Record<string, string>
  )[t] || "#6b7280";

/* -------------------------------- Zustand Store -------------------------------- */

type FlowNodeData = {
  nodeType: string;
  label: string;
  icon: string;
  settings: any;
  io: {
    inputs: { name: string; type: IOType; required?: boolean }[];
    outputs: { name: string; type: IOType }[];
  };
  mappings: { targetPath: string; expression: string; language: string }[];
};

type RNode = {
  id: string;
  type: "flowNode";
  position: { x: number; y: number };
  data: FlowNodeData;
};

type EdgeMappingRule = { targetPath: string; expression: string; language: "handlebars" | "jq" | "jmespath" };
type EdgeMapping = {
  sourceSample?: any;        // soldaki node’dan beklenen örnek çıktı (editlenebilir)
  targetTemplate?: any;      // sağdaki node input taslağı (editlenebilir)
  rules: EdgeMappingRule[];  // targetPath ⇐ expression kuralları
};

type REdge = {
  id: string;
  source: string;
  target: string;
  sourceHandle?: string;
  targetHandle?: string;
  animated?: boolean;
  label?: string;
  selected?: boolean;
  // edge tipi zaten "deletable" olarak ayarlanıyor
  mapping?: EdgeMapping;     // <—— EKLENDİ
};

type Snapshot = {
  version?: number;
  generatedAt?: string;
  pipelineId?: string; // <-- EKLE
  title?: string;      // <-- EKLE
  nodes: RNode[];
  edges: REdge[];
  viewport?: { x: number; y: number; zoom: number };
};

type Store = {
  nodes: RNode[];
  edges: REdge[];
  selectedNodeId: string | null;
  drawerOpen: boolean;
  pipelineId: string; // <-- EKLE
  title: string;      // <-- EKLE

 

  setNodes: (updater: RNode[] | ((prev: RNode[]) => RNode[])) => void;
  setEdges: (updater: REdge[] | ((prev: REdge[]) => REdge[])) => void;

  onNodesChange: (changes: any) => void;
  onEdgesChange: (changes: any) => void;
  onConnect: (connection: any) => void;

  addNode: (opts: { type: string; position: { x: number; y: number } }) => void;
  updateNodeData: (id: string, patch: Partial<FlowNodeData>) => void;
  removeNode: (id: string) => void;
  removeEdge: (id: string) => void;

  setSelectedNodeId: (id: string | null) => void;
  setDrawerOpen: (open: boolean) => void;

  exportSnapshot: () => Snapshot;
  importSnapshot: (snap: Snapshot) => void;

  edgeSettingsEdgeId: string | null;
  openEdgeSettings: (edgeId: string) => void;
  closeEdgeSettings: () => void;

  updateEdge: (id: string, patch: Partial<REdge>) => void;
  upsertEdgeMappingRule: (edgeId: string, rule: EdgeMappingRule) => void;
  removeEdgeMappingRule: (edgeId: string, idx: number) => void;
  updateEdgeMappingSamples: (edgeId: string, patch: Partial<EdgeMapping>) => void;
};

const useFlowStore = create<Store>()((set, get) => ({
  nodes: [],
  edges: [],
  selectedNodeId: null,
  drawerOpen: false,
    pipelineId: "",
  title: "",

  setNodes: (updater) =>
    set((state) => ({ nodes: typeof updater === "function" ? (updater as any)(state.nodes) : updater })),
  setEdges: (updater) =>
    set((state) => ({ edges: typeof updater === "function" ? (updater as any)(state.edges) : updater })),

  onNodesChange: (changes) => set((state) => ({ nodes: applyNodeChanges(changes, state.nodes) })),
  onEdgesChange: (changes) => set((state) => ({ edges: applyEdgeChanges(changes, state.edges) })),
onConnect: (connection) =>
  set((state) => ({
    edges: addEdge(
      { ...connection, type: "animatedDashed", selectable: true, mapping: { rules: [] } },
      state.edges
    ),
  })),


removeEdge: (id: string) =>
  set((state) => ({ edges: state.edges.filter((e) => e.id !== id) })),

  addNode: ({ type, position }) => {
    const def = NODE_TYPES_CATALOG.find((n) => n.type === type);
    if (!def) return;

    const id = uuidv4();
    const newNode: RNode = {
      id,
      type: "flowNode",
      position,
      data: {
        nodeType: type,
        label: def.label,
        icon: def.icon,
        settings: def.defaults,
        io: { 
          inputs: def.inputs as { name: string; type: IOType; required?: boolean }[], 
          outputs: def.outputs as { name: string; type: IOType }[] 
        },
        mappings: [],
      },
    };
    set((state) => ({
      nodes: [...state.nodes, newNode],
      selectedNodeId: id,
      drawerOpen: true,
    }));
  },

  updateNodeData: (id, patch) =>
    set((state) => ({
      nodes: state.nodes.map((n) => (n.id === id ? { ...n, data: { ...n.data, ...patch } } : n)),
    })),

  removeNode: (id) =>
    set((state) => ({
      nodes: state.nodes.filter((n) => n.id !== id),
      edges: state.edges.filter((e) => e.source !== id && e.target !== id),
      selectedNodeId: state.selectedNodeId === id ? null : state.selectedNodeId,
    })),

  setSelectedNodeId: (id) => set({ selectedNodeId: id, drawerOpen: !!id }),
  setDrawerOpen: (open) => set({ drawerOpen: open }),

  exportSnapshot: () => {
  const { nodes, edges } = get();
  // pipelineId ve title'ı store'da tutmak için ek alanlar ekle
  const pipelineId = get().pipelineId || "";
  const title = get().title || "";
  return {
    version: 1,
    generatedAt: new Date().toISOString(),
    pipelineId,
    title,
    nodes,
    edges,
    // viewport isteğe bağlı, ExportToolbar içinden alınabilir
  };
},

  importSnapshot: (snap) => {
    
    const fixedEdges = snap.edges.map((e: any) => {
    if (!e.sourceHandle) e.sourceHandle = "context"; // veya "request"
    if (!e.targetHandle) e.targetHandle = "context";
    return e;
  });
   
    set({
      nodes: snap.nodes as RNode[],
      edges: fixedEdges as REdge[],
      pipelineId: snap.pipelineId || "",
      title: snap.title || "",
      selectedNodeId: null,
      drawerOpen: false,
    });
  },

  edgeSettingsEdgeId: null,

  openEdgeSettings: (edgeId) => set({ edgeSettingsEdgeId: edgeId }),
  closeEdgeSettings: () => set({ edgeSettingsEdgeId: null }),

  updateEdge: (id, patch) =>
    set((state) => ({
      edges: state.edges.map((e) => (e.id === id ? { ...e, ...patch } : e)),
    })),

  upsertEdgeMappingRule: (edgeId, rule) =>
    set((state) => ({
      edges: state.edges.map((e) => {
        if (e.id !== edgeId) return e;
        const mapping: EdgeMapping = e.mapping ?? { rules: [] };
        return { ...e, mapping: { ...mapping, rules: [...mapping.rules, rule] } };
      }),
    })),

  removeEdgeMappingRule: (edgeId, idx) =>
    set((state) => ({
      edges: state.edges.map((e) => {
        if (e.id !== edgeId) return e;
        const mapping: EdgeMapping = e.mapping ?? { rules: [] };
        return {
          ...e,
          mapping: { ...mapping, rules: mapping.rules.filter((_, i) => i !== idx) },
        };
      }),
    })),

  updateEdgeMappingSamples: (edgeId, patch) =>
    set((state) => ({
      edges: state.edges.map((e) => {
        if (e.id !== edgeId) return e;
        const mapping: EdgeMapping = e.mapping ?? { rules: [] };
        return { ...e, mapping: { ...mapping, ...patch, rules: mapping.rules } };
      }),
    })),
}));

// ---------- JSON Property Editor Helpers ----------
// ---------- JSON Property Editor Helpers ----------
type J = any;

const isPlainObject = (v: J) => v !== null && typeof v === "object" && !Array.isArray(v);
const clone = <T,>(x: T): T => JSON.parse(JSON.stringify(x));

function useNodeOutputs(nodeId: string | null) {
  const [data, setData] = React.useState<Record<string, any> | null>(null);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const fetchOnce = React.useCallback(async () => {
    if (!nodeId) return;
    setLoading(true);
    setError(null);
    try {
      const r = await fetch(`${API_BASE}/api/outputs?nodeid=${encodeURIComponent(nodeId)}`, {
  headers: { Accept: "application/json" },
});
      if (!r.ok) {
        const t = await r.text().catch(() => "");
        throw new Error(t || `HTTP ${r.status}`);
      }
      const j = await r.json().catch(() => null);
      // Beklenen: { ok: true, nodeId, outputs: { handleName: value, ... } }
      setData(j?.outputs ?? null);
    } catch (e: any) {
      setError(e?.message || "Fetch error");
      setData(null);
    } finally {
      setLoading(false);
    }
  }, [nodeId]);

  // node değişince ilk çekim
  React.useEffect(() => {
    setData(null);
    setError(null);
    if (nodeId) fetchOnce();
  }, [nodeId, fetchOnce]);

  // “runCompleted” olayı sonrası otomatik refresh (+ 2 ekstra deneme)
  React.useEffect(() => {
    const onRunCompleted = () => {
      if (!nodeId) return;
      fetchOnce();
      let tries = 2;
      const iv = setInterval(() => {
        if (tries-- <= 0) return clearInterval(iv);
        fetchOnce();
      }, 1000);
    };
    window.addEventListener("flow:runCompleted", onRunCompleted);
    return () => window.removeEventListener("flow:runCompleted", onRunCompleted);
  }, [nodeId, fetchOnce]);

  return { data, loading, error, refresh: fetchOnce } as const;
}




function makeDefaultOfType(t: string): J {
  switch (t) {
    case "string": return "";
    case "number": return 0;
    case "boolean": return false;
    case "null": return null;
    case "object": return {};
    case "array": return [];
    default: return "";
  }
}

function TypeSelector({
  value,
  onTypeChange,
}: {
  value: J;
  onTypeChange: (newVal: J) => void;
}) {
  const current =
    value === null ? "null" :
    Array.isArray(value) ? "array" :
    isPlainObject(value) ? "object" : typeof value;

  return (
    <select
      className="form-select form-select-sm"
      style={{ width: 120 }}
      value={current}
      onChange={(e) => onTypeChange(makeDefaultOfType(e.target.value))}
    >
      <option value="string">string</option>
      <option value="number">number</option>
      <option value="boolean">boolean</option>
      <option value="null">null</option>
      <option value="object">object</option>
      <option value="array">array</option>
    </select>
  );
}

// TypeSelector moved to separate file for fast refresh compatibility

function PrimitiveEditor({
  value,
  onChange,
}: {
  value: string | number | boolean | null;
  onChange: (v: J) => void;
}) {
  const t = value === null ? "null" : typeof value;

  if (t === "boolean") {
    return (
      <div className="d-flex align-items-center gap-2">
        <Form.Check
          type="switch"
          checked={!!value}
          onChange={(e) => onChange(e.target.checked)}
        />
      </div>
    );
  }

  if (t === "number") {
    return (
      <Form.Control
        size="sm"
        type="number"
        value={value as number}
        onChange={(e) => onChange(Number(e.target.value))}
        style={{ maxWidth: 220 }}
      />
    );
  }

  if (t === "null") {
    return <span className="text-muted small">null</span>;
  }

  // string
  return (
    <Form.Control
      size="sm"
      type="text"
      value={value as string}
      onChange={(e) => onChange(e.target.value)}
      style={{ maxWidth: 280 }}
      spellCheck={false}
    />
  );
}

function KeyInput({
  name,
  onRename,
}: {
  name: string;
  onRename: (next: string) => void;
}) {
  const [v, setV] = React.useState(name);
  useEffect(() => setV(name), [name]);
  return (
    <Form.Control
      size="sm"
      value={v}
      onChange={(e) => setV(e.target.value)}
      onBlur={() => v !== name && onRename(v)}
      style={{ maxWidth: 220 }}
      spellCheck={false}
    />
  );
}

// ---------- Recursive JSON editor ----------
function JsonValueEditor({
  value,
  onChange,
}: {
  value: J;
  onChange: (v: J) => void;
}) {
  // primitive
  if (value === null || typeof value !== "object") {
    return (
      <div className="d-flex align-items-center gap-2">
        <PrimitiveEditor value={value} onChange={onChange} />
        <TypeSelector value={value} onTypeChange={onChange} />
      </div>
    );
  }

  // array
  if (Array.isArray(value)) {
    return (
      <div className="border rounded p-2">
        <div className="d-flex justify-content-between align-items-center mb-2">
          <div className="fw-semibold">Array [{value.length}]</div>
          <div className="d-flex gap-2">
            <Button
              size="sm"
              variant="outline-success"
              onClick={() => {
                const next = clone(value);
                next.push("");
                onChange(next);
              }}
            >
              <i className="bi bi-plus-circle" /> Add item
            </Button>
            <TypeSelector value={value} onTypeChange={onChange} />
          </div>
        </div>
        <div className="d-grid gap-2">
          {value.map((item, idx) => (
            <div key={idx} className="d-flex align-items-start gap-2 border rounded p-2">
              <Badge bg="light" text="dark">#{idx}</Badge>
              <div className="flex-grow-1">
                <JsonValueEditor
                  value={item}
                  onChange={(v) => {
                    const next = clone(value);
                    next[idx] = v;
                    onChange(next);
                  }}
                />
              </div>
              <Button
                size="sm"
                variant="outline-danger"
                onClick={() => {
                  const next = value.slice();
                  next.splice(idx, 1);
                  onChange(next);
                }}
              >
                <i className="bi bi-trash" />
              </Button>
            </div>
          ))}
        </div>
      </div>
    );
  }

  // object
  const entries = Object.entries(value as Record<string, J>);
  return (
    <div className="border rounded p-2">
      <div className="d-flex justify-content-between align-items-center mb-2">
        <div className="fw-semibold">Object {"{"}{entries.length}{"}"}</div>
        <div className="d-flex gap-2">
          <Button
            size="sm"
            variant="outline-success"
            onClick={() => {
              const next = clone(value);
              let k = "newKey";
              let i = 1;
              while (Object.prototype.hasOwnProperty.call(next, k)) {
                k = `newKey${i++}`;
              }
              (next as any)[k] = "";
              onChange(next);
            }}
          >
            <i className="bi bi-plus-circle" /> Add field
          </Button>
          <TypeSelector value={value} onTypeChange={onChange} />
        </div>
      </div>

      <div className="d-grid gap-2">
        {entries.map(([k, v]) => (
          <div key={k} className="border rounded p-2">
            <div className="d-flex align-items-start gap-2 mb-2">
              <Badge bg="secondary">{typeof v === "object" ? (Array.isArray(v) ? "array" : "object") : typeof v}</Badge>
              <KeyInput
                name={k}
                onRename={(nextName) => {
                  if (!nextName) return;
                  const next = clone(value);
                  if (nextName === k) return;
                  (next as any)[nextName] = (next as any)[k];
                  delete (next as any)[k];
                  onChange(next);
                }}
              />
              <Button
                size="sm"
                variant="outline-danger"
                className="ms-auto"
                onClick={() => {
                  const next = clone(value);
                  delete (next as any)[k];
                  onChange(next);
                }}
              >
                <i className="bi bi-trash" />
              </Button>
            </div>

            <div className="ms-3">
              <JsonValueEditor
                value={v}
                onChange={(newVal) => {
                  const next = clone(value);
                  (next as any)[k] = newVal;
                  onChange(next);
                }}
              />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}


/* -------------------------------- Custom Node -------------------------------- */
function FlowNode({ id, data }: { id: string; data: FlowNodeData }) {
  
const setSelectedNodeId = useFlowStore((s) => s.setSelectedNodeId);
  const removeNode = useFlowStore((s) => s.removeNode);
  const def = useMemo(() => NODE_TYPES_CATALOG.find((n) => n.type === data.nodeType), [data.nodeType]);


  useEffect(() => {
    if (!document.getElementById("io-align-styles")) {
      const s = document.createElement("style");
      s.id = "io-align-styles";
      s.textContent = `
        .io-row{display:flex;align-items:center;gap:.5rem;margin:2px 0}
        .io-label{font-size:12px;line-height:1.2}
        .io-row.io-in .io-badge{margin-left:auto;}
        .io-row.io-out .io-badge{margin-right:auto;}
        .io-row.io-in{justify-content:flex-start}
        .io-row.io-in .io-label{text-align:left;flex:1;}
        .io-row.io-out{justify-content:flex-end}
        .io-row.io-out .io-label{text-align:right;flex:1;}
        .io-handle{width:10px;height:10px;border-radius:999px;background:#0d6efd;border:none}
      `;
      document.head.appendChild(s);
    }
  }, []);

  return (
    <div className="card shadow-sm" style={{ width: 220 }}>
      <div className="card-body py-2 px-2">
        <div className="d-flex align-items-center justify-content-between">
          <div className="d-flex align-items-center gap-2">
            <i className={`bi ${data.icon}`} />
            <strong style={{ fontSize: 14 }}>{data.label}</strong>
          </div>
          {/* Dropdown menü */}
          <Dropdown align="end">
            <Dropdown.Toggle
              as="button"
              variant="light"
              size="sm"
              style={{ border: "none", background: "none", boxShadow: "none" }}
            >
              <i className="bi bi-three-dots-vertical" />
            </Dropdown.Toggle>
            <Dropdown.Menu>
              <Dropdown.Item onClick={() => setSelectedNodeId(id)}>
                <i className="bi bi-gear me-2" /> Settings
              </Dropdown.Item>
              <Dropdown.Item onClick={() => removeNode(id)}>
                <i className="bi bi-trash me-2" /> Delete
              </Dropdown.Item>
            </Dropdown.Menu>
          </Dropdown>
        </div>
        {/* INPUTLAR: sol handle + sol label + badge sola yaslı */}
        {def?.inputs?.length ? (
          <div className="mt-2">
            {def.inputs.map((inp,idx) => (
              <div key={inp.name} className="io-row io-in">
                <Handle
                  type="target"
                  position={Position.Left}
                  id={inp.name}
                  className="io-handle"
                  style={{ background: typeColor(inp.type as IOType),

                     top: 50 + idx * 20, // Her handle'ı aşağı kaydır
                    position: "absolute",
                    left: 0,
                   }}
                />
               
                <Badge
                  bg="secondary"
                  className="io-badge"
                  style={{
                    minWidth: 36,
                    textAlign: "left",
                    marginLeft: 0,
                    marginRight: 6,
                    padding: "2px 6px",
                    display: "inline-block"
                  }}
                >
                  {inp.type}
                </Badge>
                 <div className="io-label">{inp.name}</div>
              </div>
            ))}
          </div>
        ) : null}

        {/* ÇIKIŞLAR: label sağda, badge sağda, handle en sağda */}
        {def?.outputs?.length ? (
          <div className="mt-2">
            {def.outputs.map((out) => (
              <div key={out.name} className="io-row io-out">
                <div className="io-label">{out.name}</div>
               <Badge
                  bg="secondary"
                  className="io-badge"
                  style={{
                    minWidth: 36,
                    textAlign: "left",
                    marginLeft: 0,
                    marginRight: 6,
                    padding: "2px 6px",
                    display: "inline-block"
                  }}
                >
                  {out.type}
                </Badge>
                
                <Handle
                  type="source"
                  position={Position.Right}
                  id={out.name}
                  className="io-handle"
                  style={{ background: typeColor(out.type as IOType) }}
                />
              </div>
            ))}
          </div>
        ) : null}
      </div>
    </div>
  );
}

 
function DeletableEdge({
  id,
  selected,
  sourceX,
  sourceY,
  targetX,
  targetY,
  sourcePosition,
  targetPosition,
  markerEnd,
  style,
}: EdgeProps) {
  const removeEdge = useFlowStore((s) => s.removeEdge);
  const openEdgeSettings = useFlowStore((s) => s.openEdgeSettings);

  const [edgePath, labelX, labelY] = getBezierPath({
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
  });

  return (
    <>
      <BaseEdge id={id} path={edgePath} markerEnd={markerEnd} style={style} />
      <EdgeLabelRenderer>
        {(selected /* || true */) && (
          <div
            className="nodrag nopan"
            style={{
              position: "absolute",
              transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
              pointerEvents: "all",
              zIndex: 1000,
            }}
          >
            <div className="btn-group">
              <button
                onClick={() => openEdgeSettings(id)}
                className="btn btn-sm btn-outline-primary d-flex align-items-center gap-1"
                title="Edge settings"
                style={{ padding: "2px 8px", borderRadius: 999 }}
              >
                <i className="bi bi-gear" />
              </button>
              <button
                onClick={() => removeEdge(id)}
                className="btn btn-sm btn-danger d-flex align-items-center gap-1"
                title="Delete edge"
                style={{ padding: "2px 8px", borderRadius: 999 }}
              >
                <i className="bi bi-trash" />
              </button>
            </div>
          </div>
        )}
      </EdgeLabelRenderer>
    </>
  );
}

function EdgeSettingsModal() {
  const edgeId = useFlowStore((s) => s.edgeSettingsEdgeId);
  const close = useFlowStore((s) => s.closeEdgeSettings);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);

  const updateSamples = useFlowStore((s) => s.updateEdgeMappingSamples);
  const addRule = useFlowStore((s) => s.upsertEdgeMappingRule);
  const removeRule = useFlowStore((s) => s.removeEdgeMappingRule);

  const edge = useMemo(() => edges.find((e) => e.id === edgeId) || null, [edges, edgeId]);
  const srcNode = useMemo(() => (edge ? nodes.find((n) => n.id === edge.source) || null : null), [nodes, edge]);
  const dstNode = useMemo(() => (edge ? nodes.find((n) => n.id === edge.target) || null : null), [nodes, edge]);
// EdgeSettingsModal içinde:
const [viewMode, setViewMode] = useState<"tree"|"json">("tree");



useEffect(() => {
  if (!edge || !dstNode) return;

  // Eğer daha önce targetTemplate yoksa ve hedef node’un input tanımları varsa
  if (!edge.mapping?.targetTemplate && dstNode.data?.io?.inputs?.length) {
    const draft: any = {};
    dstNode.data.io.inputs.forEach((inp: any) => {
      switch (inp.type) {
        case "string": draft[inp.name] = ""; break;
        case "number": draft[inp.name] = 0; break;
        case "boolean": draft[inp.name] = false; break;
        case "object": draft[inp.name] = {}; break;
        case "array": draft[inp.name] = []; break;
        default: draft[inp.name] = null;
      }
    });
    // Store güncelle
    useFlowStore.getState().updateEdgeMappingSamples(edge.id, { targetTemplate: draft });
  }
}, [edge?.id, dstNode?.id]);



  const [newTargetPath, setNewTargetPath] = useState("");
  const [newExpr, setNewExpr] = useState("");
  const [newLang, setNewLang] = useState<"handlebars" | "jq" | "jmespath">("handlebars");

  if (!edge) return null;

  const mapping = edge.mapping ?? { rules: [] };
  const sourceTitle = srcNode ? `${srcNode.data.label} → outputs` : "Source";
  const targetTitle = dstNode ? `${dstNode.data.label} → inputs` : "Target";

  return (
    <Modal show={!!edgeId} onHide={close} size="xl" centered>
      <Modal.Header closeButton>
        <Modal.Title>
          <i className="bi bi-gear me-2" />
          Edge Settings
          <Badge bg="light" text="dark" className="ms-2">{edge.id}</Badge>
        </Modal.Title>
      </Modal.Header>

      <Modal.Body>
  {/* Header actions'ı değil, Body içeriğini buraya koyacağız */}

  <div className="d-flex gap-2 mb-3">
    <Form.Check
      type="switch"
      label={viewMode === "tree" ? "Tree" : "JSON"}
      checked={viewMode === "json"}
      onChange={() => setViewMode(m => (m === "tree" ? "json" : "tree"))}
    />
  </div>

  {/* Body: sol & sağ panel */}
  <div className="d-flex gap-3">
    <div className="flex-fill">
      <div className="fw-semibold mb-2">Source</div>
      {viewMode === "tree" ? (
        <JsonTree value={mapping.sourceSample ?? {}} side="source" />
      ) : (
        <div className="border rounded p-2">
          <JsonValueEditor
            value={mapping.sourceSample ?? {}}
            onChange={(v) => updateSamples(edge.id, { sourceSample: v })}
          />
        </div>
      )}
    </div>

    <div className="flex-fill">
      <div className="fw-semibold mb-2">Target</div>
      {viewMode === "tree" ? (
        <JsonTree value={mapping.targetTemplate ?? {}} side="target" onDropPath={handleDropToTarget} />
      ) : (
        <div className="border rounded p-2">
          <JsonValueEditor
            value={mapping.targetTemplate ?? {}}
            onChange={(v) => updateSamples(edge.id, { targetTemplate: v })}
          />
        </div>
      )}
    </div>
  </div>
</Modal.Body>


      <Modal.Footer>
        <Button variant="secondary" onClick={close}>
          Close
        </Button>
      </Modal.Footer>
    </Modal>
  );

  function handleDropToTarget(targetPath: string, sourcePath: string) {
  if (edge) {
    addRule(edge.id, {
      targetPath,
      expression: sourcePath,       // örn: jmespath için doğrudan path
      language: "jmespath",
    });
  }
}
 
}
 

function AnimatedDashedEdge(props) {
  const [edgePath, labelX, labelY] = getBezierPath(props);
  const removeEdge = useFlowStore((s) => s.removeEdge);
  const openEdgeSettings = useFlowStore((s) => s.openEdgeSettings);

  useEffect(() => {
    if (!document.getElementById("dashed-edge-anim-style")) {
      const style = document.createElement("style");
      style.id = "dashed-edge-anim-style";
      style.textContent = `
        .animated-dashed-edge {
          stroke-dasharray: 8 6;
          stroke-width: 2.5;
          stroke: #0d6efd;
          animation: dashmove 1s linear infinite;
        }
        @keyframes dashmove {
          to { stroke-dashoffset: -14; }
        }
      `;
      document.head.appendChild(style);
    }
  }, []);

  return (
    <>
      <BaseEdge
        path={edgePath}
        markerEnd={props.markerEnd}
        style={{ pointerEvents: "stroke" }}
        className="animated-dashed-edge"
      />
      <EdgeLabelRenderer>
        {props.selected && (
          <div
            className="nodrag nopan"
            style={{
              position: "absolute",
              transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
              pointerEvents: "all",
              zIndex: 1000,
              display: "flex",
              gap: 4,
            }}
          >
            <button
              onClick={() => openEdgeSettings(props.id)}
              className="btn btn-sm btn-light d-flex align-items-center"
              title="Edge settings"
              style={{ padding: "2px 8px", borderRadius: 999 }}
            >
              <i className="bi bi-gear" />
            </button>
            <button
              onClick={() => removeEdge(props.id)}
              className="btn btn-sm btn-danger d-flex align-items-center"
              title="Delete edge"
              style={{ padding: "2px 8px", borderRadius: 999 }}
            >
              <i className="bi bi-trash" />
            </button>
          </div>
        )}
      </EdgeLabelRenderer>
    </>
  );
}

const nodeTypes = { flowNode: FlowNode };
const edgeTypes = { 
  animatedDashed: AnimatedDashedEdge,
  deletable: DeletableEdge };

/* -------------------------------- Left Catalog -------------------------------- */

function NodeCatalog() {
  const onDragStart = (event: React.DragEvent, nodeType: string) => {
    event.dataTransfer.setData("application/reactflow", nodeType);
    event.dataTransfer.effectAllowed = "move";
  };
  const cats = useMemo(() => {
    const map = new Map<string, typeof NODE_TYPES_CATALOG>();
    NODE_TYPES_CATALOG.forEach((n) => {
      if (!map.has(n.category)) map.set(n.category, [] as any);
      (map.get(n.category) as any[]).push(n);
    });
    return [...map.entries()];
  }, []);

  return (
    <div
      className="p-2"
      style={{ width: 280, borderRight: "1px solid #e5e7eb", height: "100%", overflowY: "auto" }}
    >
      <div className="d-flex align-items-center justify-content-between mb-2">
        <h6 className="mb-0">Nodes</h6>
      </div>
      <Tabs defaultActiveKey={cats[0]?.[0] || "All"} className="mb-3">
        {cats.map(([cat, arr]) => (
          <Tab
            key={cat}
            eventKey={cat}
            title={
              <span>
                {cat}{" "}
                <Badge bg="light" text="dark">
                  {arr.length}
                </Badge>
              </span>
            }
          >
            <div className="mt-2 d-grid gap-2">
              {arr.map((n) => (
                <div
                  key={n.type}
                  className="border rounded p-2 d-flex align-items-center gap-2 bg-light"
                  draggable
                  onDragStart={(e) => onDragStart(e, n.type)}
                >
                  <i className={`bi ${n.icon}`} />
                  <div>
                    <div className="fw-semibold" style={{ fontSize: 13 }}>
                      {n.label}
                    </div>
                    <small className="text-muted">{n.type}</small>
                  </div>
                </div>
              ))}
            </div>
          </Tab>
        ))}
      </Tabs>
      <div className="small text-muted">
        <i className="bi bi-info-circle me-1" /> Sürükleyip tuvale bırakın.
      </div>
    </div>
  );
}

function OutputsTab() {
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const { data, loading, error, refresh } = useNodeOutputs(selectedNodeId);

  if (!selectedNodeId) {
    return <div className="text-muted small">Seçili node yok.</div>;
  }

  return (
    <div className="d-grid gap-2">
      <div className="d-flex align-items-center gap-2">
        <div className="fw-semibold">Node:</div>
        <code>{selectedNodeId}</code>
        <div className="ms-auto d-flex gap-2">
          <Button size="sm" variant="outline-secondary" onClick={refresh} disabled={loading}>
            {loading ? "Yükleniyor…" : "Yenile"}
          </Button>
        </div>
      </div>

      {error && (
        <div className="alert alert-danger py-2 mb-0 small">
          {error}
        </div>
      )}

      {!error && !loading && !data && (
        <div className="text-muted small">Çıktı bulunamadı.</div>
      )}

      {!error && data && (
        <div className="d-grid gap-2">
          {Object.keys(data).length === 0 ? (
            <div className="text-muted small">Handle bulunamadı.</div>
          ) : (
            Object.entries(data).map(([handle, value]) => (
              <div key={handle} className="border rounded p-2">
                <div className="d-flex align-items-center gap-2 mb-2">
                  <Badge bg="light" text="dark">{handle}</Badge>
                  <div className="ms-auto">
                    <Button
                      size="sm"
                      variant="outline-dark"
                      onClick={async () => {
                        try {
                          const r = await fetch(`/api/outputs/${encodeURIComponent(selectedNodeId)}?handle=${encodeURIComponent(handle)}`);
                          const j = await r.json().catch(() => null);
                          const v = j?.value ?? value;
                          await navigator.clipboard.writeText(JSON.stringify(v, null, 2));
                          alert("Kopyalandı.");
                        } catch {
                          alert("Kopyalama başarısız.");
                        }
                      }}
                    >
                      <i className="bi bi-clipboard" /> Kopyala
                    </Button>
                  </div>
                </div>
                {/* JSON görüntüleme */}
                <div className="border rounded p-2 bg-light">
                  <pre className="mb-0 small" style={{ whiteSpace: "pre-wrap" }}>
                    {safeJSONString(value)}
                  </pre>
                </div>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  );
}

function safeJSONString(v: any) {
  try { return JSON.stringify(v, null, 2); } catch { return String(v); }
}



/* -------------------------------- Settings Drawer -------------------------------- */

function SettingsDrawer() {
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const drawerOpen = useFlowStore((s) => s.drawerOpen);
  const setDrawerOpen = useFlowStore((s) => s.setDrawerOpen);
  const nodes = useFlowStore((s) => s.nodes);
  const updateNodeData = useFlowStore((s) => s.updateNodeData);

  const node = useMemo(() => nodes.find((n) => n.id === selectedNodeId) || null, [nodes, selectedNodeId]);

  const [settingsStr, setSettingsStr] = useState("");
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [newMapTarget, setNewMapTarget] = useState("");
  const [newMapExpr, setNewMapExpr] = useState("");
  const [newMapLang, setNewMapLang] = useState("handlebars");

  useEffect(() => {
    if (node?.data?.settings) {
      setSettingsStr(JSON.stringify(node.data.settings, null, 2));
      setSettingsError(null);
    } else {
      setSettingsStr("{}");
    }
  }, [node?.id]);

  const saveSettings = () => {
    if (!node) return;
    try {
      const parsed = JSON.parse(settingsStr);
      updateNodeData(node.id, { settings: parsed });
      setSettingsError(null);
    } catch (e: any) {
      setSettingsError(e.message || "Invalid JSON");
    }
  };

  const addMapping = () => {
    if (!node || !newMapTarget || !newMapExpr) return;
    const next = [
      ...(node.data.mappings || []),
      { targetPath: newMapTarget, expression: newMapExpr, language: newMapLang },
    ];
    updateNodeData(node.id, { mappings: next });
    setNewMapTarget("");
    setNewMapExpr("");
  };

  const removeMapping = (idx: number) => {
    if (!node) return;
    const next = (node.data.mappings || []).filter((_, i) => i !== idx);
    updateNodeData(node.id, { mappings: next });
  };

  return (
    <Offcanvas show={drawerOpen} onHide={() => setDrawerOpen(false)} placement="start" style={{ width: 420 }}>
      <Offcanvas.Header closeButton>
        <Offcanvas.Title>
          {node ? (
            <span className="d-inline-flex align-items-center gap-2">
              <i className={`bi ${node.data.icon}`} />
              {node.data.label}
              <Badge bg="light" text="dark" className="ms-2">
                {node.data.nodeType}
              </Badge>
            </span>
          ) : (
            "Node Settings"
          )}
        </Offcanvas.Title>
      </Offcanvas.Header>
      <Offcanvas.Body>
        {!node ? (
          <div className="text-muted">Seçili node yok.</div>
        ) : (
          <>
            <Tabs defaultActiveKey="settings" className="mb-3">
              <Tab eventKey="outputs" title="Outputs">
                <OutputsTab />
              </Tab>
              <Tab eventKey="settings" title="Settings (JSON)">
                <Form.Group className="mb-2">
                  <Form.Label className="fw-semibold">Settings JSON</Form.Label>
                  <Form.Control
                    as="textarea"
                    rows={12}
                    value={settingsStr}
                    onChange={(e) => setSettingsStr(e.target.value)}
                    spellCheck={false}
                  />
                  {settingsError ? <div className="text-danger small mt-1">{settingsError}</div> : null}
                </Form.Group>
                <div className="d-flex gap-2">
                  <Button variant="primary" onClick={saveSettings}>
                    <i className="bi bi-save me-1" />
                    Save
                  </Button>
                </div>
              </Tab>
              
                    <Tab eventKey="propedit" title="Property Editor">
                      {!node ? null : (
                        <div className="d-grid gap-2">
                          <div className="small text-muted">
                            Anahtar adı, tip ve değerleri görsel olarak düzenleyin. Değişiklikler anında kaydedilir.
                          </div>
                          <JsonValueEditor
                            value={node.data.settings ?? {}}
                            onChange={(v) => updateNodeData(node.id, { settings: v })}
                          />
                          <div className="alert alert-light border small mt-2 mb-0">
                            <i className="bi bi-info-circle me-1" />
                            Metin düzenleme için <strong>Settings (JSON)</strong> sekmesini kullanabilirsiniz.
                          </div>
                        </div>
                      )}
                    </Tab>
               

              <Tab eventKey="io" title="Inputs / Outputs">
                <div className="mb-2">
                  <div className="fw-semibold mb-1">Inputs</div>
                  {node.data.io.inputs?.length ? (
                    node.data.io.inputs.map((inp) => (
                      <div key={inp.name} className="d-flex align-items-center gap-2 border rounded p-2 mb-2">
                        <Badge style={{ background: typeColor(inp.type as IOType) }}>{inp.type}</Badge>
                        <div className="ms-1 fw-semibold">{inp.name}</div>
                        <div className="small text-muted ms-auto">
                          required: {inp.required ? "yes" : "no"}
                        </div>
                      </div>
                    ))
                  ) : (
                    <div className="text-muted small">No inputs</div>
                  )}
                </div>

                <div className="mb-2">
                  <div className="fw-semibold mb-1">Outputs</div>
                  {node.data.io.outputs?.length ? (
                    node.data.io.outputs.map((out) => (
                      <div key={out.name} className="d-flex align-items-center gap-2 border rounded p-2 mb-2">
                        <Badge style={{ background: typeColor(out.type as IOType) }}>{out.type}</Badge>
                        <div className="ms-1 fw-semibold">{out.name}</div>
                      </div>
                    ))
                  ) : (
                    <div className="text-muted small">No outputs</div>
                  )}
                </div>
              </Tab>

              <Tab eventKey="mappings" title="Data Mappings">
                <div className="mb-2">
                  <div className="fw-semibold mb-1">New mapping</div>
                  <InputGroup className="mb-2">
                    <InputGroup.Text>target</InputGroup.Text>
                    <Form.Control
                      placeholder="e.g. response.users"
                      value={newMapTarget}
                      onChange={(e) => setNewMapTarget(e.target.value)}
                    />
                  </InputGroup>
                  <InputGroup className="mb-2">
                    <InputGroup.Text>expr</InputGroup.Text>
                    <Form.Control
                      placeholder="e.g. users[].name"
                      value={newMapExpr}
                      onChange={(e) => setNewMapExpr(e.target.value)}
                    />
                  </InputGroup>
                  <Form.Select
                    className="mb-2"
                    value={newMapLang}
                    onChange={(e) => setNewMapLang(e.target.value)}
                  >
                    <option value="handlebars">Handlebars</option>
                    <option value="jq">jq</option>
                    <option value="jmespath">JMESPath</option>
                  </Form.Select>
                  <Button variant="success" onClick={addMapping}>
                    <i className="bi bi-plus-circle me-1" />
                    Add mapping
                  </Button>
                </div>

                <div className="mt-3">
                  <div className="fw-semibold mb-1">Existing mappings</div>
                  {node.data.mappings?.length ? (
                    node.data.mappings.map((m, idx) => (
                      <div key={`${m.targetPath}-${idx}`} className="d-flex align-items-center gap-2 border rounded p-2 mb-2">
                        <Badge bg="light" text="dark">{m.language}</Badge>
                        <div className="ms-1">
                          <div className="fw-semibold">{m.targetPath}</div>
                          <div className="small text-muted">{m.expression}</div>
                        </div>
                        <Button
                          size="sm"
                          variant="outline-danger"
                          className="ms-auto"
                          onClick={() => removeMapping(idx)}
                        >
                          <i className="bi bi-x-lg" />
                        </Button>
                      </div>
                    ))
                  ) : (
                    <div className="text-muted small">No mappings</div>
                  )}
                </div>
              </Tab>
            </Tabs>
          </>
        )}
      </Offcanvas.Body>
    </Offcanvas>
  );
}

/* -------------------------------- Export / Import Toolbar -------------------------------- */

 function ExportToolbar() {
  const exportSnapshot = useFlowStore((s) => s.exportSnapshot);
  const importSnapshot = useFlowStore((s) => s.importSnapshot);
  const { getViewport, setViewport } = useReactFlow();

  // Modal state
  const [flowsModalOpen, setFlowsModalOpen] = useState(false);
  const [execResult, setExecResult] = useState<any>(null);
  const [execModalOpen, setExecModalOpen] = useState(false);
  const [exportModalOpen, setExportModalOpen] = useState(false);
  const [saveModalOpen, setSaveModalOpen] = useState(false);

  // Export/Save için pipelineId ve title state
  const [pipelineIdInput, setPipelineIdInput] = useState("");
  const [titleInput, setTitleInput] = useState("");

const [testModalOpen, setTestModalOpen] = useState(false);
//const [seeds, setSeeds] = useState<Array<{ nodeId: string; handle: string; value: string }>>([]);
const [testPayload, setTestPayload] = useState<string>('{}');
 const [testNodeId, setTestNodeId] = useState("");
// Test modalını aç
const openTestModal = () => setTestModalOpen(true);

  // Export JSON popup
  const handleExport = () => setExportModalOpen(true);
  const doExport = () => {
    const data = exportSnapshot();
    data.pipelineId = pipelineIdInput || data.pipelineId || uuidv4();
    data.title = titleInput || data.title || "Untitled";
    const full = { ...data, viewport: getViewport?.() };
    const blob = new Blob([JSON.stringify(full, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `flow-snapshot-${new Date().toISOString().replace(/[:.]/g, "-")}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setExportModalOpen(false);
  };

  // Save Flow popup
  const handleSaveFlow = () => setSaveModalOpen(true);
  const doSaveFlow = async () => {
    const data = exportSnapshot();
    data.pipelineId = pipelineIdInput || data.pipelineId || uuidv4();
    data.title = titleInput || data.title || "Untitled";
    await fetch(`${API_BASE}/api/flows/save`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: data.title, snapshot: data }),
    });
    setSaveModalOpen(false);
    alert("Flow saved ✔");
  };

  // Open Flow
  const handleSelectFlow = (snapshot: any) => {
    setFlowsModalOpen(false);
    
    importSnapshot(snapshot);
  };

  // Import JSON
  const handleImportFile = async (file: File) => {
    const text = await file.text();
    const snap = JSON.parse(text) as Snapshot;
    importSnapshot(snap);
    if (snap.viewport) setViewport?.(snap.viewport);
  };
  const onPickFile = () => {
    const inp = document.createElement("input");
    inp.type = "file";
    inp.accept = "application/json";
    inp.onchange = () => {
      const file = inp.files?.[0];
      if (file) handleImportFile(file).catch((e) => alert(e.message));
    };
    inp.click();
  };

  // Copy JSON
  const handleCopy = async () => {
    const data = exportSnapshot();
    const full = { ...data, viewport: getViewport?.() };
    await navigator.clipboard.writeText(JSON.stringify(full, null, 2));
    alert("JSON panoya kopyalandı.");
  };

  // LocalStorage
  const saveToLocal = () => {
    const data = exportSnapshot();
    const full = { ...data, viewport: getViewport?.() };
    localStorage.setItem("flow-snapshot", JSON.stringify(full));
    alert("LocalStorage'a kaydedildi.");
  };
  const loadFromLocal = () => {
    const raw = localStorage.getItem("flow-snapshot");
    if (!raw) return alert("LocalStorage'da kayıt bulunamadı.");
    const snap = JSON.parse(raw) as Snapshot;
    importSnapshot(snap);
    if (snap.viewport) setViewport?.(snap.viewport);
  };

  // Backend işlemleri
  // const runInBackend = async () => {
  //   const data = exportSnapshot();
  //   const resp = await fetch(`${API_BASE}/api/execute`, {
  //     method: "POST",
  //     headers: { "Content-Type": "application/json" },
  //     body: JSON.stringify({ snapshot: data, seed: [] }),
  //   });
  //   const json = await resp.json();
  //   setExecResult(json);
  //   setExecModalOpen(true);
  //   window.dispatchEvent(new CustomEvent("flow:runCompleted", { detail: json }));
  // };

// Run (Backend) -> seeds'i body'ye ekle
const runInBackend = async () => {
  const data = exportSnapshot();
  let payloadObj;
  try {
    payloadObj = JSON.parse(testPayload);
  } catch {
    alert("Payload geçerli bir JSON olmalı!");
    return;
  }
  const payload = {
    snapshot: data,
    payload: payloadObj,
  };

  try {
    const resp = await fetch(`${API_BASE}/api/execute`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    const json = await resp.json().catch(() => ({}));
    setExecResult(json);
    setExecModalOpen(true);

    window.dispatchEvent(new CustomEvent("flow:runCompleted", { detail: json }));
  } catch (err: any) {
    setExecResult({ error: err?.message || "Network error" });
    setExecModalOpen(true);
  }
};

  const loadTriggers = async () => {
    const data = exportSnapshot();
    await fetch(`${API_BASE}/api/snapshot`, {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(data)
    });
    const resp = await fetch(`${API_BASE}/api/triggers/load`, { method: "POST" });
    const json = await resp.json();
    alert("Triggers mapped on host.");
  };

  const manualInject = async () => {
    const nodeId = prompt("Node ID?");
    const handle = prompt("Handle name?", "request");
    const value  = prompt("Seed JSON?", '{"hello":"world"}');
    if (!nodeId || !handle) return;
    const resp = await fetch(`${API_BASE}/api/inject`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ nodeId, handle, value: value ? JSON.parse(value) : null }),
    });
    const json = await resp.json();
    alert(json.ok ? "Injected ✔" : "Inject failed ❌");
  };

  // Export/Save modalı
  const renderExportSaveModal = (show: boolean, onHide: () => void, onConfirm: () => void, title: string) => (
    <Modal show={show} onHide={onHide} centered>
      <Modal.Header closeButton>
        <Modal.Title>{title}</Modal.Title>
        {/* Flow Execution Results
        {Array.isArray(result?.nodes) && result.nodes.some((x:any)=>x.error) && (
          <Badge bg="danger" className="ms-2">Errors</Badge>
        )} */}
      </Modal.Header>
      <Modal.Body>
        <Form.Group className="mb-3">
          <Form.Label>Pipeline ID</Form.Label>
          <Form.Control
            value={pipelineIdInput}
            onChange={e => setPipelineIdInput(e.target.value)}
            placeholder="pipelineId (otomatik üretilecek)"
          />
        </Form.Group>
        <Form.Group>
          <Form.Label>Title</Form.Label>
          <Form.Control
            value={titleInput}
            onChange={e => setTitleInput(e.target.value)}
            placeholder="Flow title"
          />
        </Form.Group>
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>Cancel</Button>
        <Button variant="primary" onClick={onConfirm}>OK</Button>
      </Modal.Footer>
    </Modal>
  );

  return (
    <div className="d-flex gap-2 p-2 border-bottom bg-white position-sticky top-0" style={{ zIndex: 5 }}>
      <Button size="sm" variant="outline-primary" onClick={handleExport}>
        <i className="bi bi-download me-1" /> Export JSON
      </Button>
      <Button size="sm" variant="outline-secondary" onClick={handleCopy}>
        <i className="bi bi-clipboard me-1" /> Copy JSON
      </Button>
      <Button size="sm" variant="outline-success" onClick={onPickFile}>
        <i className="bi bi-upload me-1" /> Import JSON
      </Button>
      <Button size="sm" variant="outline-dark" onClick={saveToLocal}>
        <i className="bi bi-save me-1" /> Save (Local)
      </Button>
      <Button size="sm" variant="outline-dark" onClick={loadFromLocal}>
        <i className="bi bi-box-arrow-in-down me-1" /> Load (Local)
      </Button>
      <Button size="sm" variant="primary" onClick={runInBackend}>
        <i className="bi bi-play-fill me-1" /> Run (Backend)
      </Button>
      <Button size="sm" variant="outline-primary" onClick={loadTriggers}>
        <i className="bi bi-plug me-1" /> Map Triggers
      </Button>
      <Button size="sm" variant="outline-dark" onClick={manualInject}>
        <i className="bi bi-lightning me-1" /> Inject Event
      </Button>
      <Button size="sm" variant="outline-info" onClick={() => setFlowsModalOpen(true)}>
        <i className="bi bi-folder2-open me-1" /> Open Flows
      </Button>
      <Button size="sm" variant="outline-success" onClick={handleSaveFlow}>
        <i className="bi bi-save2 me-1" /> Save Flow
      </Button>
     <Button size="sm" variant="outline-warning" onClick={openTestModal}>
  <i className="bi bi-beaker me-1" /> Test Run
</Button>
      {/* Modal bileşenleri */}
      {renderExportSaveModal(exportModalOpen, () => setExportModalOpen(false), doExport, "Export JSON")}
      {renderExportSaveModal(saveModalOpen, () => setSaveModalOpen(false), doSaveFlow, "Save Flow")}
      <FlowsModal show={flowsModalOpen} onHide={() => setFlowsModalOpen(false)} onSelectFlow={handleSelectFlow} />
      <ExecuteResultModal show={execModalOpen} onHide={() => setExecModalOpen(false)} result={execResult} />
      <TestRunModal
  show={testModalOpen}
  onHide={() => setTestModalOpen(false)}
  payload={testPayload}
  setPayload={setTestPayload}
  nodeId={testNodeId}
  setNodeId={setTestNodeId}
/>
    </div>
  );
}


function TestRunModal({
  show,
  onHide,
  payload,
  setPayload,
}: {
  show: boolean;
  onHide: () => void;
  payload: string;
  setPayload: React.Dispatch<React.SetStateAction<string>>;
}) {
  return (
    <Modal show={show} onHide={onHide} size="lg" centered>
      <Modal.Header closeButton>
        <Modal.Title>Test Run – Payload Gir</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        <div className="small text-muted mb-2">
          Flow'u test etmek için JSON payload girin. Bu body, execute sırasında <code>payload</code> olarak gönderilecek.
        </div>
        <Form.Control
          as="textarea"
          rows={8}
          className="font-monospace"
          placeholder='{"hello":"world"}'
          value={payload}
          onChange={e => setPayload(e.target.value)}
          spellCheck={false}
        />
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>Kapat</Button>
      </Modal.Footer>
    </Modal>
  );
}
// FlowsModal zaten import/export formatını kullanıyor, ek değişiklik gerekmez.




/* -------------------------------- Canvas -------------------------------- */

function FlowCanvas() {
  const { screenToFlowPosition } = useReactFlow();
  const setSelectedNodeId = useFlowStore((s) => s.setSelectedNodeId);
  const setDrawerOpen = useFlowStore((s) => s.setDrawerOpen);
  const addNode = useFlowStore((s) => s.addNode);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const onNodesChange = useFlowStore((s) => s.onNodesChange);
  const onEdgesChange = useFlowStore((s) => s.onEdgesChange);
  const onConnect = useFlowStore((s) => s.onConnect);

  // Hata highlight için execute sonuçlarını dinle
  const [errorNodeIds, setErrorNodeIds] = useState<string[]>([]);
  useEffect(() => {
    const handler = (e: any) => {
      if (e?.detail?.nodes) {
        const ids = e.detail.nodes.filter((n: any) => n.error).map((n: any) => n.id);
        setErrorNodeIds(ids);
      } else {
        setErrorNodeIds([]);
      }
    };
    window.addEventListener("flow:runCompleted", handler);
    return () => window.removeEventListener("flow:runCompleted", handler);
  }, []);

  const onDragOver = useCallback((event: React.DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
  }, []);

  const onDrop = useCallback(
    (event: React.DragEvent) => {
      event.preventDefault();
      const type = event.dataTransfer.getData("application/reactflow");
      if (!type) return;
      const pos = screenToFlowPosition({ x: event.clientX, y: event.clientY });
      addNode({ type, position: pos });
      setDrawerOpen(true);
    },
    [addNode, screenToFlowPosition, setDrawerOpen]
  );

  const onPaneClick = useCallback(() => setSelectedNodeId(null), [setSelectedNodeId]);

  // Node'ları hata varsa border ile highlight et
  const nodesWithError = useMemo(() => {
    if (!errorNodeIds.length) return nodes;
    return nodes.map((n) =>
      errorNodeIds.includes(n.id)
        ? { ...n, style: { border: "2px solid red", boxShadow: "0 0 8px #dc3545" } }
        : n
    );
  }, [nodes, errorNodeIds]);

  return (
    <div className="flex-fill" style={{ height: "100%" }} onDragOver={onDragOver} onDrop={onDrop}>
      <ReactFlow
        defaultEdgeOptions={{ type: "deletable", selectable: true }}
        elementsSelectable
        nodes={nodesWithError}
        edges={edges}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onPaneClick={onPaneClick}
        fitView
      >
        <MiniMap />
        <Controls />
        <Background />
      </ReactFlow>
    </div>
  );
}


 

 

/* -------------------------------- Shell -------------------------------- */

function App() {
  return (
    <div className="d-flex" style={{ height: "100vh" }}>
      <NodeCatalog />
      <div className="flex-fill position-relative" style={{ height: "100%" }}>
        {/* Provider içinde toolbar + canvas + drawer */}
        <ReactFlowProvider>
          <ExportToolbar />
          <FlowCanvas />
          <SettingsDrawer />
            <EdgeSettingsModal />
        </ReactFlowProvider>
      </div>
    </div>
  );
}

/* -------------------------------- Mount -------------------------------- */

const container = document.getElementById("root");
if (!container) {
  const div = document.createElement("div");
  div.id = "root";
  document.body.style.margin = "0";
  document.body.appendChild(div);
  createRoot(div).render(<App />);
} else {
  createRoot(container).render(<App />);
}

// components/JsonTree.tsx
function JsonTree({
  value, side, onDropPath,
}: { value: any; side: "source" | "target"; onDropPath?: (targetPath: string, sourcePath: string) => void }) {
  const items = useMemo(() => listJsonPaths(value ?? {}, ""), [value]);

  return (
    <div className="border rounded p-2 small" style={{ maxHeight: 420, overflow: "auto" }}>
      {items.map(({ path, type }) => {
        const isLeaf = !["object", "array"].includes(type);
        return (
          <div
            key={`${side}-${path}`}
            className="d-flex align-items-center justify-content-between py-1 px-2 rounded hover-bg-light"
            draggable={side === "source" && isLeaf}
            onDragStart={(e) => side === "source" && e.dataTransfer.setData("text/plain", path)}
            onDragOver={(e) => {
              if (side === "target") { e.preventDefault(); }
            }}
            onDrop={(e) => {
              if (side === "target" && onDropPath) {
                e.preventDefault();
                const src = e.dataTransfer.getData("text/plain");
                if (src) onDropPath(path, src);
              }
            }}
          >
            <span className="text-monospace">{path || "$"}</span>
            <Badge bg="light" text="dark">{type}</Badge>
          </div>
        );
      })}
    </div>
  );
}

// utils/jsonPath.ts
export function listJsonPaths(obj: any, base = ""): { path: string; type: string }[] {
  const out: { path: string; type: string }[] = [];
  const push = (p: string, v: any) => {
    const t = v === null ? "null" : Array.isArray(v) ? "array" : typeof v === "object" ? "object" : typeof v;
    out.push({ path: p || "$", type: t });
  };
  const walk = (v: any, p: string) => {
    if (v === null || typeof v !== "object") { push(p, v); return; }
    if (Array.isArray(v)) {
      push(p, v);
      v.forEach((item, i) => walk(item, `${p}[${i}]`));
    } else {
      push(p, v);
      Object.keys(v).forEach(k => walk(v[k], p ? `${p}.${k}` : k));
    }
  };
  walk(obj, base);
  return out;
}

function FlowsModal({
  show,
  onHide,
}: {
  show: boolean;
  onHide: () => void;
}) {
  const [flows, setFlows] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const importSnapshot = useFlowStore((s) => s.importSnapshot);

  useEffect(() => {
    if (!show) return;
    setLoading(true);
    fetch(`${API_BASE}/api/flows`)
      .then((r) => r.json())
      .then((j) => setFlows(j.items || []))
      .catch(() => setFlows([]))
      .finally(() => setLoading(false));
  }, [show]);

  // Flow seçilince dosya içeriğini oku ve doğrudan import et
  const handleSelect = async (flow: any) => {
    try {
      const resp = await fetch(`${API_BASE}/api/flows/${flow.pipelineId}`);
      const json = await resp.json();
      
      const snapshotv=JSON.parse(json.snapshot);
      json.snapshot=snapshotv;
         // Eski sürüm uyumluluğu
      if (!json.snapshot || !Array.isArray(json.snapshot.nodes) || !Array.isArray(json.snapshot.edges)) {
        alert("No snapshot found.");
        return;
      }
      importSnapshot(json.snapshot); // Doğrudan store'a import et
      onHide();
    } catch {
      alert("Flow yüklenemedi.");
    }
  };

  return (
    <Modal show={show} onHide={onHide} size="lg" centered>
      <Modal.Header closeButton>
        <Modal.Title>Open Flow</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {loading ? (
          <Spinner animation="border" />
        ) : (
          <div className="d-grid gap-2">
            {flows.map((f) => (
              <Button key={f.pipelineId} variant="outline-primary" onClick={() => handleSelect(f)}>
                <div className="fw-semibold">{f.title}</div>
                <div className="small text-muted">{f.pipelineId}</div>
              </Button>
            ))}
            {!flows.length && <div className="text-muted">No flows found.</div>}
          </div>
        )}
      </Modal.Body>
    </Modal>
  );
}

function ExecuteResultModal({ show, onHide, result }: { show: boolean; onHide: () => void; result: any }) {
  if (!result) return null;
  return (
    <Modal show={show} onHide={onHide} size="xl" centered>
      <Modal.Header closeButton>
        <Modal.Title>Flow Execution Results</Modal.Title>
      </Modal.Header>
      <Modal.Body>
        {result.nodes?.map((n: any) => (
          <div key={n.id} className={`border rounded p-2 mb-2 ${n.error ? "border-danger bg-light" : ""}`}>
            <div className="d-flex align-items-center gap-2">
              <Badge bg={n.error ? "danger" : "secondary"}>{n.id}</Badge>
              <span className="fw-semibold">{n.label}</span>
              {n.error && <span className="text-danger ms-2">Error: {n.error}</span>}
            </div>
            <div className="mt-2">
              <div className="small fw-semibold">Inputs</div>
              <pre className="bg-light p-2 rounded">{safeJSONString(n.inputs)}</pre>
              <div className="small fw-semibold">Outputs</div>
              <pre className="bg-light p-2 rounded">{safeJSONString(n.outputs)}</pre>
            </div>
          </div>
        ))}
        {!result.nodes?.length && <div className="text-muted">No node results.</div>}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="secondary" onClick={onHide}>Close</Button>
      </Modal.Footer>
    </Modal>
  );
}