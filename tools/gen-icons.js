// Generates IconCatalog.cs from Lucide icon data (ISC licensed, https://lucide.dev).
// Usage: node tools/gen-icons.js
const fs = require("fs");
const path = require("path");
const https = require("https");

const root = path.resolve(__dirname, "..");
const dataFile = path.join(__dirname, "lucide-icon-nodes.json");
const outFile = path.join(root, "IconCatalog.cs");

const categories = [
  ["Health", [
    "heart", "heart-pulse", "heart-handshake", "activity", "stethoscope", "pill", "syringe",
    "thermometer", "brain", "dumbbell", "apple", "leaf", "cross", "ambulance", "hospital",
    "bed", "baby", "footprints", "eye", "microscope"
  ]],
  ["Office", [
    "briefcase", "briefcase-business", "building", "warehouse", "file-text", "files", "folder",
    "folder-open", "mail", "inbox", "send", "clipboard", "clipboard-list", "paperclip", "pen-line",
    "notebook-pen", "notebook", "book-open", "users", "user", "id-card", "phone", "archive",
    "presentation", "layout-grid", "layout-dashboard", "grid-2x2"
  ]],
  ["Time", [
    "clock", "alarm-clock", "timer", "hourglass", "watch", "calendar-clock", "calendar-check",
    "calendar-days", "clock-arrow-up", "sunrise", "sunset", "sun", "moon", "moon-star"
  ]],
  ["Marketing", [
    "megaphone", "target", "trending-up", "chart-line", "chart-column", "chart-pie", "rocket",
    "lightbulb", "globe", "share-2", "thumbs-up", "shopping-cart", "shopping-bag", "badge-percent",
    "tag", "tags", "ticket", "hash", "mail-open", "at-sign", "repeat", "zap", "gift", "star",
    "flag", "award", "trophy", "crown", "medal", "sparkles", "wand-sparkles", "calculator"
  ]],
  ["Plotter / Design", [
    "pen-tool", "pencil", "pencil-ruler", "palette", "brush", "paintbrush", "scissors", "ruler",
    "shapes", "layers", "crop", "image", "frame", "component", "drafting-compass", "vector-square",
    "grid-3x3", "scan", "maximize", "spline"
  ]],
  ["Printing", [
    "printer", "printer-check", "file-output", "copy", "package", "folder-down", "scan-text",
    "stamp", "truck", "clipboard-copy", "file-check", "boxes", "package-open", "file-down"
  ]],
  ["Files / Dev", [
    "terminal", "square-terminal", "code", "code-xml", "database", "server", "cloud", "download",
    "upload", "network", "cpu", "hard-drive", "keyboard", "mouse", "usb", "wifi", "monitor",
    "laptop", "smartphone", "tablet", "camera", "video", "mic", "headphones", "audio-lines",
    "gamepad-2", "speaker"
  ]],
  ["Utilities", [
    "settings", "wrench", "shield", "lock", "key", "search", "house", "bookmark", "link",
    "map-pin", "plane", "car", "coffee", "utensils", "external-link", "funnel", "list", "check",
    "plus", "info", "circle-question-mark", "triangle-exclamation-point", "bell", "power",
    "log-out", "refresh-cw", "app-window"
  ]],
];

function shapeToPath(node) {
  const [tag, a = {}] = node;
  const n = v => Number(v);
  switch (tag) {
    case "path":
      return a.d || "";
    case "line":
      return `M ${a.x1} ${a.y1} L ${a.x2} ${a.y2}`;
    case "circle": {
      const cx = n(a.cx), cy = n(a.cy), r = n(a.r);
      return `M ${cx - r} ${cy} A ${r} ${r} 0 1 1 ${cx + r} ${cy} A ${r} ${r} 0 1 1 ${cx - r} ${cy} Z`;
    }
    case "ellipse": {
      const cx = n(a.cx), cy = n(a.cy), rx = n(a.rx), ry = n(a.ry);
      return `M ${cx - rx} ${cy} A ${rx} ${ry} 0 1 1 ${cx + rx} ${cy} A ${rx} ${ry} 0 1 1 ${cx - rx} ${cy} Z`;
    }
    case "rect": {
      const x = n(a.x), y = n(a.y), w = n(a.width), h = n(a.height);
      return `M ${x} ${y} H ${x + w} V ${y + h} H ${x} Z`;
    }
    case "polyline":
    case "polygon": {
      const nums = String(a.points || "").trim().split(/[\s,]+/).map(Number);
      if (nums.length < 4) return "";
      let d = `M ${nums[0]} ${nums[1]}`;
      for (let i = 2; i + 1 < nums.length; i += 2) d += ` L ${nums[i]} ${nums[i + 1]}`;
      if (tag === "polygon") d += " Z";
      return d;
    }
    default:
      return "";
  }
}

function titleCase(name) {
  return name.split("-").map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(" ");
}

function download(url, dest, cb) {
  https.get(url, res => {
    if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
      res.resume();
      return download(res.headers.location, dest, cb);
    }
    if (res.statusCode !== 200) {
      console.error("HTTP " + res.statusCode + " for " + url);
      process.exit(1);
    }
    const out = fs.createWriteStream(dest);
    res.pipe(out);
    out.on("finish", () => out.close(cb));
  }).on("error", err => { console.error(err); process.exit(1); });
}

function escapeCs(s) {
  return s.replace(/\\/g, "\\\\").replace(/"/g, '\\"');
}

function generate() {
  const data = JSON.parse(fs.readFileSync(dataFile, "utf8"));
  const entries = [];
  const missing = [];

  for (const [category, names] of categories) {
    for (const name of names) {
      const nodes = data[name];
      if (!nodes) { missing.push(name); continue; }
      const d = nodes.map(shapeToPath).filter(Boolean).join(" ").trim();
      if (!d) { missing.push(name + " (no geometry)"); continue; }
      entries.push({ key: "lucide:" + name, name: titleCase(name), category, path: d });
    }
  }

  const lines = [];
  lines.push("// <auto-generated>");
  lines.push("// Generated by tools/gen-icons.js from Lucide icon data (https://lucide.dev, ISC license).");
  lines.push("// Do not edit by hand.");
  lines.push("// </auto-generated>");
  lines.push("namespace RightDock");
  lines.push("{");
  lines.push("    internal static class IconCatalog");
  lines.push("    {");
  lines.push("        internal sealed class Entry");
  lines.push("        {");
  lines.push("            internal Entry(string key, string name, string category, string path)");
  lines.push("            {");
  lines.push("                Key = key;");
  lines.push("                Name = name;");
  lines.push("                Category = category;");
  lines.push("                Path = path;");
  lines.push("            }");
  lines.push("");
  lines.push("            internal string Key { get; }");
  lines.push("            internal string Name { get; }");
  lines.push("            internal string Category { get; }");
  lines.push("            internal string Path { get; }");
  lines.push("        }");
  lines.push("");
  lines.push("        internal const string DefaultKey = \"lucide:app-window\";");
  lines.push("        internal const string AppIconKey = \"app\";");
  lines.push("");
  lines.push("        internal static readonly Entry[] Icons =");
  lines.push("        {");
  for (const e of entries) {
    lines.push(`            new Entry("${escapeCs(e.key)}", "${escapeCs(e.name)}", "${escapeCs(e.category)}", "${escapeCs(e.path)}"),`);
  }
  lines.push("        };");
  lines.push("    }");
  lines.push("}");

  fs.writeFileSync(outFile, lines.join("\r\n") + "\r\n");
  console.log(`Wrote ${entries.length} icons to ${outFile}`);
  if (missing.length) console.log("Missing from Lucide: " + missing.join(", "));
}

if (!fs.existsSync(dataFile)) {
  console.log("Downloading Lucide icon data...");
  download("https://unpkg.com/lucide-static@latest/icon-nodes.json", dataFile, generate);
} else {
  generate();
}
