# PixelGridRecovery

AI 이미지 생성기의 pixel art는 시각적으로는 픽셀아트여도 실제 raster에는 확대된 셀, 미세한 색 변화, grid offset이 남아 있을 수 있습니다. PixelGridRecovery V0.1은 이미지의 반복 경계를 분석하고, 완전한 격자 셀마다 대표색 하나를 선택해 작은 sprite PNG로 복원하는 Windows Forms 도구입니다.

```text
Input PNG/JPG
  → Edge Signal (X / Y)
  → Grid Period Detection
  → Grid Offset Detection
  → Grid-aligned Crop
  → Block Reduction
  → Pixel-perfect Output PNG

1254 × 1254 → 19 × 19 grid, offset (0, 0) → 66 × 66
```

## 실행과 빌드

- Windows 10/11, .NET 8 SDK 또는 .NET 8을 대상으로 빌드할 수 있는 최신 SDK.
- 프레임워크 종속 실행에는 **.NET 8 Desktop Runtime**이 필요합니다. .NET 10 런타임만 설치한 경우 .NET 8 런타임을 대신하지 않습니다.
- Core는 `net8.0`이며 UI 및 System.Drawing 의존성이 없습니다. App과 이미지 입출력 테스트는 `net8.0-windows`입니다.
- 외부 이미지 처리 패키지를 사용하지 않습니다. 테스트 패키지는 xUnit과 Microsoft.NET.Test.Sdk입니다.

저장소 루트에서:

```powershell
dotnet restore PixelGridRecovery.sln
dotnet build PixelGridRecovery.sln -c Release --no-restore
dotnet test PixelGridRecovery.sln -c Release --no-build --no-restore
dotnet run --project src/PixelGridRecovery.App
```

`global.json`은 .NET 8 이상 SDK를 선택하도록 설정되어 있습니다. Windows 외 환경에서는 Core 테스트만 실행할 수 있습니다:

```powershell
dotnet test tests/PixelGridRecovery.Tests
```

별도 런타임 설치 없이 실행하는 Windows x64 배포본:

```powershell
dotnet publish src/PixelGridRecovery.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/PixelGridRecovery-v0.1-win-x64
```

`artifacts/PixelGridRecovery-v0.1-win-x64/PixelGridRecovery.App.exe`를 실행합니다. 배포본은 런타임을 포함하므로 소스 빌드보다 크며, 첫 실행 시 네이티브 런타임 파일을 사용자 임시 영역에 풉니다.

개발 PC에 .NET 8 런타임이 없어 이 작업에서는 Microsoft의 [공식 설치 스크립트](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script)로 `.tools/dotnet`에만 런타임을 준비했습니다. 이 로컬 런타임으로 테스트할 때는:

```powershell
$env:DOTNET_ROOT = (Resolve-Path '.tools/dotnet').Path
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
dotnet test PixelGridRecovery.sln --no-build --no-restore
```

`.tools`와 `artifacts`는 생성물이며 Git에 포함하지 않습니다.

## 사용법

1. **Load Image**로 PNG/JPG를 엽니다.
2. **Auto Detect**를 누릅니다. Cell Width/Height, Offset X/Y, Confidence가 갱신됩니다.
3. 원본 위 청록색 **Grid Overlay**가 실제 블록 경계와 맞는지 확인합니다. 체크박스로 격자를 숨길 수 있습니다.
4. 필요하면 셀 크기와 오프셋을 직접 수정합니다. 가로와 세로 크기를 각각 지정할 수 있으며, 오프셋 범위는 `0 ~ 해당 셀 크기 - 1`입니다.
5. **Reduction Mode**를 선택합니다. 기본값은 **Median**입니다.
6. **Preview Result**를 누르면 완전한 셀만 크롭하고 축소합니다. Original / Cropped / Output 해상도가 표시됩니다.
7. 결과를 확인하고 **Export PNG**로 저장합니다. 기본 파일명은 `원본이름-recovered.png`입니다.

숫자나 축소 방식을 바꾸면 이전 결과는 지워지고 Export가 비활성화됩니다. Preview Result를 다시 누른 후 저장합니다. 오버레이는 화면에서만 그리며 원본 이미지 데이터에 포함되지 않습니다. 미리보기는 nearest-neighbor로 창에 맞추고, 확대 시 정수 배율을 사용합니다. 너무 작은 격자는 화면에서 일부 선을 생략합니다. 이미지 픽셀과 저장 결과에는 영향을 주지 않습니다.

Confidence가 낮아도 수동 처리할 수 있습니다. 완전한 셀이 하나도 남지 않는 설정은 안내 문구를 표시하고 미리보기를 막습니다. 파일을 읽은 뒤 파일 핸들을 해제하며, PNG 저장은 같은 폴더의 임시 파일에 인코딩을 완료한 다음 대상 파일을 교체합니다.

## 검출 알고리즘

`GridDetector`는 이미지 너비·높이의 약수를 사용하지 않습니다.

1. 모든 인접 픽셀의 RGB 및 alpha 차이를 축별로 평균 내어 `D_x(x)`, `D_y(y)`를 계산합니다. RGB는 alpha를 곱한 값으로 비교해 완전 투명 영역의 숨은 RGB를 무시합니다.
2. 경계 신호의 하위 20%, 40% 분위수로 셀 내부 잡음 수준을 추정해 차감합니다. 최소 대비보다 약한 잔여 잡음은 버리고, 남은 신호의 75% 분위수로 상한을 제한하여 고립된 큰 경계의 영향을 줄입니다.
3. 기본 후보 `p = 2..64`, `offset = 0..p-1`를 X/Y 독립적으로 검사합니다. 관측 가능한 반복 경계가 최소 3개 있어야 후보가 됩니다. 이미지 시작점에는 이전 픽셀이 없으므로 0 위치의 경계는 점수에서 제외합니다.
4. 각 후보에 대해 **경계 위치의 평균 강도**와 **전체 경계 신호 중 해당 위치가 설명하는 비율**의 조화평균을 계산합니다. 평균 강도는 실제 경계가 나타나는 구간에서 구해 넓은 빈 여백의 영향을 줄입니다. 작은 주기는 셀 내부의 약한 신호 때문에, 큰 주기는 건너뛴 경계 때문에 감점됩니다.
5. 최선 점수, 차선 점수와의 차이, 대비 수준으로 0~1 Confidence를 계산합니다. X/Y 중 낮은 값을 사용합니다. 이는 통계적 정답 확률이 아닌 상대적인 품질 지표입니다.

검출은 결정론적입니다. 충분한 증거가 없으면 기본 셀 크기 2(1px 축은 1), 오프셋 0, 낮은 Confidence를 반환합니다. 검색 범위는 `GridDetectionOptions`에서 변경할 수 있으며, 주요 점수 상수는 `GridDetector`에 모아 두었습니다.

## 크롭과 대표색 규칙

`startX = OffsetX`, `cropWidth = floor((Width - OffsetX) / CellWidth) * CellWidth`이며 Y축도 같습니다. 왼쪽/위쪽의 부분 셀과 오른쪽/아래쪽의 남는 부분을 제거합니다. 크롭과 축소는 새 이미지에 기록하며 입력을 바꾸지 않습니다.

| 모드 | RGB | Alpha |
| --- | --- | --- |
| Center | `(CellWidth / 2, CellHeight / 2)` 픽셀. 짝수 셀에서는 가운데 네 픽셀 중 오른쪽 아래 | 선택한 픽셀의 A |
| Average | `Σ(channel × alpha) / Σ(alpha)`. 투명 RGB의 오염 방지 | 모든 픽셀의 A 산술평균 |
| Median (기본) | A > 0인 픽셀들의 R/G/B 중앙값을 각각 계산 | A = 0을 포함한 모든 픽셀의 A 중앙값 |

나눗셈은 가장 가까운 정수로 반올림하고 정확히 절반이면 올립니다. 짝수 개의 중앙값은 가운데 두 값의 평균입니다. 최종 A가 0이면 RGB도 0으로 정규화합니다. 모든 계산은 원본의 8비트 채널 값에서 수행하며 별도 색 공간 변환은 하지 않습니다. Median은 투명 픽셀이 과반인 블록에서 완전히 투명한 결과를 낼 수 있습니다.

## 구조

```text
src/
  PixelGridRecovery.Core/
    PixelImage.cs / Rgba32.cs        RGBA 이미지 모델
    GridInfo.cs                     격자 크기, 위상, 신뢰도
    GridDetectionOptions.cs
    GridDetector.cs                  경계 기반 주기/위상 검출
    GridBounds.cs / GridCropper.cs   완전한 셀만 크롭
    BlockReductionMode.cs
    BlockReducer.cs                 Center / Average / Median
    ImageProcessingService.cs       UI 없이 실행 가능한 전체 처리 흐름
  PixelGridRecovery.App/
    BitmapCodec.cs                  System.Drawing 파일 입출력/모델 변환
    ImagePreviewControl.cs          화면 전용 격자와 nearest-neighbor 미리보기
    MainForm.cs / MainForm.Layout.cs
    Program.cs
tests/
  PixelGridRecovery.Tests/          Core 합성 이미지 테스트
  PixelGridRecovery.App.Tests/      Windows PNG/JPG 및 폼 렌더링 테스트
```

UI를 통하지 않는 처리 예:

```csharp
var service = new ImageProcessingService();
GridInfo grid = service.Detect(input); // input: PixelImage
ProcessingResult result = service.Process(input, grid, BlockReductionMode.Median);
PixelImage sprite = result.Output;
```

## 테스트

- 8×8 논리 픽셀을 10×10 실제 픽셀로 확대한 80×80 이미지.
- 패딩 오프셋 (3, 7), ±4~5 RGB 잡음, 비정사각형 셀, 2/19/64px 주기.
- 1254×1254 → 19px 격자 → 66×66 PNG 파일 입출력과 전체 픽셀 비교.
- 단색·거의 단색·단일 경계·큰 배경·고립된 강한 선·투명 RGB·alpha 경계.
- 크롭의 배수 해상도, 오프셋, 원본 보존, 잘못된 격자 거절.
- 세 대표색 방식, 짝수 중앙값, 반투명/완전 투명 RGB 처리와 해상도.
- PNG RGBA 왕복, JPG 로드, 파일 잠금 해제, 잘못된 파일, 저장 교체/실패 정리.
- 기본/최소 창 크기에서 폼 생성·렌더링과 도구 모음 배치.

## V0.1 한계

- 이미지 전체에서 일정한 **정수 크기·축 정렬 격자**를 가정합니다. 비정수 확대, 회전, 원근, 휘어진 경계, 영역별 다른 배율은 복원하지 않습니다.
- 자동 검색의 기본 범위는 축별 2~64px입니다. 수동 입력은 1px부터 이미지 크기까지 가능합니다.
- 반복 경계가 적거나 배경 비중이 크면 신뢰도가 낮아집니다. 원래 격자보다 굵은 경계만 남았다면 실제 논리 해상도를 유일하게 알아낼 수 없습니다.
- 이웃 블록 경계의 흐림, JPEG 압축, 강한 잡음, 불규칙한 픽셀 형태가 있으면 수동 보정이 필요할 수 있습니다. 합성 이미지로 검증했으며 모든 AI 생성 이미지의 정답을 보장하지 않습니다.
- V0.1 입력 상한은 16,777,216 픽셀입니다. 처리는 명시적 버튼 클릭 때 동기 실행하므로 큰 이미지나 작은 셀에서는 잠시 기다릴 수 있습니다.
- 8비트 RGBA로 읽고 PNG로 저장합니다. EXIF/ICC 등 메타데이터나 원본의 고비트 심도는 보존하지 않습니다.
- AI/딥러닝, 배경 제거, 애니메이션, 팔레트 최적화, dithering, batch 처리, Unity 연동, segmentation은 포함하지 않습니다.
