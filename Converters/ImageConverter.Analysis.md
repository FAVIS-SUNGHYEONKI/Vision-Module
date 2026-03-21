# ImageConverter 메서드 분석

## ToCogImage8Grey(Mat mat)

### 역할
OpenCV의 `Mat` → VisionPro의 `CogImage8Grey` 변환

---

### 1단계 — 입력 검증

```csharp
if (mat == null || mat.Empty())
    throw new ArgumentNullException(nameof(mat));
```

null이거나 픽셀 데이터가 없는 Mat은 이후 처리 불가능하므로 즉시 차단한다.

---

### 2단계 — 그레이스케일 확보

```csharp
bool converted = mat.Channels() > 1;
Mat gray = converted ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat;
```

- `CogImage8Grey`는 **1채널(8bit 그레이)만** 받을 수 있다.
- 채널이 1개(이미 그레이)면 → `mat` 그대로 사용, 새 Mat 할당 없음
- 채널이 3개(BGR 컬러)면 → 새 그레이 Mat을 생성
- `converted` 플래그는 나중에 **새로 만든 Mat만 Dispose** 하기 위해 기억해 둔다.

---

### 3단계 — CogImage 버퍼 생성

```csharp
var cogImg = new CogImage8Grey();
cogImg.Allocate(gray.Width, gray.Height);
```

VisionPro 이미지 객체를 만들고 가로×세로 크기만큼 내부 픽셀 버퍼를 할당한다.
이 시점엔 버퍼가 비어 있다.

---

### 4단계 — 픽셀 메모리 잠금

```csharp
pixMem = cogImg.Get8GreyPixelMemory(
    CogImageDataModeConstants.ReadWrite, 0, 0, gray.Width, gray.Height);
```

VisionPro는 픽셀 버퍼에 직접 접근할 때 **잠금(lock)** 을 요구한다.

| 파라미터 | 의미 |
|---|---|
| `ReadWrite` | 쓰기 목적으로 잠금 |
| `0, 0` | 시작 좌표 (x, y) |
| `gray.Width, gray.Height` | 접근할 영역 크기 |

반환되는 `pixMem`은 내부 버퍼의 시작 주소(`Scan0`)와 행 간격(`Stride`)을 제공한다.

---

### 5단계 — 픽셀 데이터 복사

```csharp
byte[] row = new byte[gray.Width];
for (int y = 0; y < gray.Height; y++)
{
    Marshal.Copy(gray.Ptr(y), row, 0, gray.Width);
    Marshal.Copy(row, 0, pixMem.Scan0 + y * pixMem.Stride, gray.Width);
}
```

행 하나씩 반복하며 **두 번의 Marshal.Copy**로 복사한다.

```
[Mat 비관리 메모리]  →  [row: 관리 메모리]  →  [CogImage 비관리 메모리]
  gray.Ptr(y)                byte[]             pixMem.Scan0 + y * Stride
```

`Stride`가 필요한 이유: 메모리 정렬로 인해 실제 행 간격이 `Width`보다 클 수 있다.
예) width=100이면 stride=128처럼 패딩이 붙을 수 있다.

---

### 6단계 — 잠금 해제 / 중간 Mat 정리

```csharp
finally { pixMem?.Dispose(); }
finally { if (converted) gray.Dispose(); }
```

- `pixMem.Dispose()` — 잠금 해제 + 변경 사항 커밋.
  이걸 빠뜨리면 복사한 픽셀 데이터가 `cogImg`에 반영되지 않는다.
- 원본 `mat`은 호출자가 소유하므로 건드리지 않는다.
- 2단계에서 새로 만든 그레이 Mat(`converted == true`)만 해제한다.

---

### 전체 흐름 요약

```
Mat (컬러 or 그레이)
 │
 ├─ 컬러면 → CvtColor → gray Mat (새로 할당)
 │
 ├─ CogImage8Grey 생성 + Allocate
 │
 ├─ Get8GreyPixelMemory → 픽셀 버퍼 잠금
 │
 ├─ 행 단위 Marshal.Copy (Mat → byte[] → CogImage)
 │
 ├─ pixMem.Dispose() → 잠금 해제 + 커밋
 │
 └─ 임시 gray Mat Dispose → CogImage8Grey 반환
```

### Get8GreyPixelMemory vs Bitmap 경유 방식

| 항목 | Get8GreyPixelMemory | Bitmap 경유 |
|---|---|---|
| 메모리 복사 횟수 | **1회** (Mat → Cog) | 2회 (Mat → Bitmap → Cog) |
| 중간 힙 할당 | 없음 | Bitmap 객체 (GDI+ 힙) |
| GDI+ 리소스 | 없음 | LockBits / UnlockBits 필요 |
| 8bpp Bitmap 제약 | 없음 | Indexed 포맷 → LockBits 불가한 경우 있음 |
| Stride 정렬 문제 | VisionPro 내부 stride만 처리 | GDI+ 4바이트 정렬 + VisionPro stride 이중 처리 |
| 멀티스레드 | 문제 없음 | GDI+ Bitmap은 스레드 안전하지 않음 |

---

## ToMat(CogImage8Grey cogImg) — private 헬퍼

### 역할
`ToCogImage8Grey`의 **정확한 역방향** — `CogImage8Grey` → `Mat(CV_8UC1)`

---

### ToCogImage8Grey와 대칭 구조

두 메서드를 나란히 놓으면 완전히 대칭이다.

```
ToCogImage8Grey:   Mat.Ptr(y) → byte[] → pixMem.Scan0 + y * Stride
ToMat(헬퍼):       pixMem.Scan0 + y * Stride → byte[] → mat.Ptr(y)
```

복사 방향만 반대이고 나머지 구조는 동일하다.

---

### 1단계 — Mat 버퍼 생성

```csharp
var mat = new Mat(cogImg.Height, cogImg.Width, MatType.CV_8UC1);
```

OpenCV Mat을 생성한다. `CV_8UC1` = 8bit, 1채널(그레이스케일).
`ToCogImage8Grey`의 `cogImg.Allocate()`에 대응한다.

---

### 2단계 — 픽셀 메모리 잠금

```csharp
pixMem = cogImg.Get8GreyPixelMemory(
    CogImageDataModeConstants.Read, 0, 0, cogImg.Width, cogImg.Height);
```

`ToCogImage8Grey`와 차이점:

| | ToCogImage8Grey | ToMat 헬퍼 |
|---|---|---|
| 모드 | `ReadWrite` | **`Read`** |
| 이유 | CogImage에 데이터를 씀 | CogImage에서 데이터를 읽기만 함 |

`Read` 모드는 VisionPro 내부적으로 더 가벼운 잠금을 사용한다.

---

### 3단계 — 픽셀 데이터 복사

```csharp
byte[] row = new byte[cogImg.Width];
for (int y = 0; y < cogImg.Height; y++)
{
    Marshal.Copy(pixMem.Scan0 + y * pixMem.Stride, row, 0, cogImg.Width);
    Marshal.Copy(row, 0, mat.Ptr(y), cogImg.Width);
}
```

```
[CogImage 비관리 메모리]  →  [row: 관리 메모리]  →  [Mat 비관리 메모리]
  Scan0 + y * Stride             byte[]                 mat.Ptr(y)
```

`Stride` 주의: CogImage는 행마다 패딩이 있을 수 있지만 Mat은 없다.
`y * pixMem.Stride`로 CogImage 행을 정확히 찾고, `Width`만큼만 읽어서 패딩을 건너뛴다.

---

### 4단계 — 잠금 해제

```csharp
finally { pixMem?.Dispose(); }
```

읽기 모드였더라도 반드시 해제한다.
해제하지 않으면 `cogImg`가 잠긴 상태로 남아 다른 VisionPro 툴에서 접근할 수 없게 된다.

---

### private인 이유

외부에서는 `ICogImage`를 받는 public `ToMat(ICogImage)`를 사용한다.
이 헬퍼는 타입이 이미 확정된 상태에서만 호출되므로 내부 구현 세부사항으로 숨겨둔 것이다.

---

### 전체 흐름 요약

```
CogImage8Grey
 │
 ├─ Mat(CV_8UC1) 생성
 │
 ├─ Get8GreyPixelMemory(Read) → 픽셀 버퍼 잠금
 │
 ├─ 행 단위 Marshal.Copy (CogImage → byte[] → Mat)
 │   └─ Stride로 패딩 건너뜀
 │
 ├─ pixMem.Dispose() → 잠금 해제
 │
 └─ Mat 반환
```
