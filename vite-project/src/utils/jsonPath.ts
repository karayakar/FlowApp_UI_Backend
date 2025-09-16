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
