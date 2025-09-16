# FlowApp: Sürükle-Bırak Akış Tasarım Platformu

[![Tanıtım Videosu](https://img.youtube.com/vi/vd8gF7tGDCo/0.jpg)](https://youtu.be/vd8gF7tGDCo)
> 📺 [Tanıtım videosunu izlemek için tıklayın](https://youtu.be/vd8gF7tGDCo)

---

  <iframe width="1399" height="1469" src="https://www.youtube.com/embed/vd8gF7tGDCo" title="Karay akar flow (n8n like flow full app)" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" referrerpolicy="strict-origin-when-cross-origin" allowfullscreen></iframe>

## Proje Hakkında

**FlowApp**, görsel olarak sürükle-bırak yöntemiyle iş akışları (flow/pipeline) tasarlamanıza olanak sağlayan, modern ve modüler bir platformdur.  
Kullanıcılar, hazır node’ları kullanarak frontend arayüzde akışlarını oluşturur, backend ise bu akışları çalıştırır ve yönetir.

UI : http://localhost:5173/
Backend: https://localhost:53750/

---

## Özellikler

### 🚀 Genel Özellikler

- Sürükle-bırak ile görsel akış tasarımı
- 40+ hazır node (veri, ağ, kontrol, AI, tetikleyici, dosya, vb.)
- Her node için giriş/çıkış (input/output) handle’ları
- Edge (bağlantı) mapping ve animasyonlu, silinebilir bağlantılar
- Akışları kaydetme, yükleme, dışa aktarma
- Akış çalıştırma ve canlı sonuç izleme
- JSON tabanlı snapshot desteği
- Paralel ve dallanabilen akış yürütme
- Gelişmiş loglama ve hata yönetimi

---

### 🖥️ Frontend (React + React Flow)

- Modern, responsive arayüz (React + TypeScript)
- Node katalogu: Sürükle-bırak ile ekleme
- Her node için ayar paneli (settings drawer)
- Giriş/çıkış handle’ları için etiket ve tip rozetleri
- Edge’ler için animasyonlu kesik çizgi ve silme/ayar butonları
- Akış görselleştirme, zoom, pan, grid
- Akışları JSON olarak dışa aktarma ve içe aktarma
- Node ve edge mapping desteği
- Kapsamlı node türleri (aşağıda listelenmiştir)

---

### 🖧 Backend (C# .NET)

- Akış snapshot’larını çalıştıran engine
- Her node için asenkron işlem desteği
- Paralel ve dallanabilen yürütme (bağımsız node’lar eşzamanlı çalışır)
- Edge mapping ve input-output zinciri yönetimi
- Merge (birleştirici) node ve özel node desteği
- Gelişmiş loglama ve hata yönetimi
- HTTP API ile frontend entegrasyonu

---

## 🔗 Node Kataloğu (Toplam: **40+** node)

Aşağıdaki kategorilerde node’lar mevcuttur:

### **Veri & Dönüşüm**
- JSON Transform
- JSON Merge
- String Concat
- String Template
- Regex Extract
- Math Operation
- Array Filter / Map

### **Ağ & Entegrasyon**
- HTTP Request
- HTTP Listen
- WebSocket Send / In
- Webhook In / Verify
- Email Send
- MQTT Subscribe
- SSE Subscribe
- Queue Consume

### **Dosya & Depolama**
- Read File / Write File
- Cache Get / Set

### **Kontrol & Akış**
- If
- Switch
- Loop
- Delay
- Try / Catch

### **AI & NLP**
- OpenAI Chat
- Text-to-Speech (TTS)
- Speech-to-Text (STT)

### **Tetikleyiciler**
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

> **Not:** Her node’un giriş/çıkış handle’ları, tip rozetleri ve ayar paneli vardır.

---

## 🛠️ Kurulum

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

---

## 📦 Kullanım

1. Frontend arayüzde node’ları sürükleyip bırakın.
2. Node’lar arası bağlantıları (edge) oluşturun.
3. Her node’un ayarlarını ve mapping’lerini düzenleyin.
4. Akışı kaydedin veya dışa aktarın.
5. Çalıştırmak için backend’e gönderin ve sonuçları izleyin.

---

## 👨‍💻 Katkı ve Geliştirme

- Yeni node eklemek için `NODE_TYPES_CATALOG` ve backend’de `INodeProcessor` implementasyonu ekleyin.
- UI/UX geliştirmeleri için React ve React Flow bileşenlerini düzenleyin.
- Hataları ve önerileri [issue açarak](https://github.com/senin-repon/issues) bildirebilirsiniz.

---

## 📹 Video

[![Tanıtım Videosu](https://img.youtube.com/vi/XXXXXXXXXXX/0.jpg)](https://www.youtube.com/watch?v=XXXXXXXXXXX)

---

**FlowApp ile iş akışlarınızı kolayca tasarlayın, yönetin ve otomatikleştirin!**
