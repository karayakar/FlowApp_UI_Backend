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
