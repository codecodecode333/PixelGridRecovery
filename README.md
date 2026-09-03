# PixelGridRecovery

확대·리샘플링된 픽셀아트의 격자를 추정하고 원래 논리 픽셀 단위의 PNG로 복원하는 C# / .NET 8 / Windows Forms 도구입니다. V0.2는 **Fractional Grid Recovery**를 지원합니다.

```text
PNG/JPG
  → X/Y edge signals (한 번 계산)
  → 기존 정수 detector + 관측 경계 간격으로 coarse 후보 생성
  → fractional period / offset refinement
  → geometry / complete-cell validation
  → 원본 fractional cell 직접 sampling
  → logical sprite PNG
```

예를 들어 실제 간격이 18.6px이면 64번째 경계는 1190.4px입니다. 19로 반올림해 반복하면 1216px까지 이동해 25.6px의 오차가 생깁니다. 새 경로는 모든 경계에 `offset + index * period`를 사용하며 셀마다 반올림하거나 중간 resize를 수행하지 않습니다.

## 실행과 빌드

Windows 10/11, .NET 8을 대상으로 빌드할 수 있는 SDK가 필요합니다. 프레임워크 종속 실행에는 **.NET 8 Desktop Runtime**이 필요합니다. 설치된 .NET 10 런타임만으로 .NET 8 앱이 실행되지는 않습니다.

```powershell
dotnet restore PixelGridRecovery.sln
dotnet build PixelGridRecovery.sln -c Release --no-restore
dotnet test PixelGridRecovery.sln -c Release --no-build --no-restore
dotnet run --project src/PixelGridRecovery.App
```

Core는 UI 및 System.Drawing에 의존하지 않는 `net8.0` 라이브러리입니다. App과 PNG/JPG·폼 테스트는 `net8.0-windows`입니다. 외부 이미지/수치 처리 라이브러리는 사용하지 않습니다. 테스트는 xUnit입니다.

Core 테스트만 실행하려면:

```powershell
dotnet test tests/PixelGridRecovery.Tests
```

별도 런타임 설치 없이 실행하는 Windows x64 배포본:

```powershell
dotnet publish src/PixelGridRecovery.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/PixelGridRecovery-v0.2-win-x64
```

생성된 `PixelGridRecovery.App.exe`를 실행합니다. 런타임 포함 배포본은 크기가 크며 첫 실행 시 네이티브 런타임 파일을 사용자 임시 영역에 풉니다.

이 작업 공간에는 [Microsoft 공식 설치 스크립트](https://learn.microsoft.com/dotnet/core/tools/dotnet-install-script)로 준비한 로컬 런타임 `.tools/dotnet`도 있습니다. 이를 사용하는 테스트 명령은 다음과 같습니다. `.tools`와 `artifacts`는 Git에 포함되지 않습니다.

```powershell
$env:DOTNET_ROOT = (Resolve-Path '.tools/dotnet').Path
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
dotnet test PixelGridRecovery.sln -c Release --no-build --no-restore
```

## 사용법

1. **Load Image**로 PNG/JPG를 엽니다.
2. **Auto Detect**로 셀 크기·오프셋·검출 방식·신뢰도를 확인합니다.
3. 청록색 **Grid Overlay**가 처음뿐 아니라 오른쪽과 아래쪽 끝에서도 경계와 맞는지 확인합니다.
4. 필요하면 **Cell Width / Height / Offset X / Y**를 직접 수정합니다. 소수점 3자리 표시, 증감 단위 0.05px이며 자동 검출의 내부 정밀도는 유지합니다.
5. **Reduction Mode**를 선택합니다. 기본값은 **DominantColor**입니다.
6. **Preview Result**로 원본의 실수 셀 영역을 직접 축소합니다.
7. **Export PNG**로 저장합니다. 기본 파일명은 `원본이름-recovered.png`입니다.

기존 Load → Auto Detect → Overlay → Preview → Export 흐름을 유지합니다. 격자/모드가 바뀌면 이전 결과를 지우고 Export를 비활성화합니다. 신뢰도가 낮아도 수동 처리가 가능합니다. 완전한 셀이 하나도 남지 않는 설정은 거절합니다.

Original은 입력 raster 크기, Cropped는 사용한 실수 영역의 크기, Output은 정수 논리 해상도입니다. 화면은 nearest-neighbor로 창에 맞추고 확대 시 정수 배율을 사용합니다. 축소된 화면에서 너무 촘촘한 격자는 일부 선을 생략하지만, 각 선은 실수 geometry에서 직접 계산합니다. 원본 데이터에는 선을 그리지 않습니다.

## 모델과 호환성

기존 `GridInfo(int, int, int, int, confidence)`, `GridDetector.Detect`, `ImageProcessingService.Detect`, 정수 `GridCropper` 및 `BlockReducer` API는 유지합니다. 기존 API가 실수 결과를 조용히 반올림하지 않습니다.

실수 검출/복원에는 다음 모델과 API를 사용합니다:

```csharp
public sealed record GridGeometry(
    double CellWidth, double CellHeight,
    double OffsetX, double OffsetY,
    double Confidence = 0,
    GridDetectionMethod Method = GridDetectionMethod.Manual);

var service = new ImageProcessingService();
GridDetectionResult detection = service.DetectDetailed(input); // PixelImage
GridGeometry geometry = detection.Geometry;
// geometry = new GridGeometry(18.55, 18.70, 7.35, 12.7); // 수동 입력
GridRecoveryResult result = service.Process(input, geometry);
PixelImage sprite = result.Output;
```

`DetectGeometry`는 geometry만 반환합니다. `DetectDetailed`는 각 축의 coarse period/offset, 동일 기준으로 계산한 integer score, refined period/offset/score, 지지 경계 수, 공간 범위, 위상 RMS 오차와 이미지 경계 보정 여부를 반환합니다.

UI는 Method, 셀 크기, 오프셋, Confidence, Output을 표시합니다. Debug 빌드에서는 `Debug.WriteLine`으로 축별 진단 정보도 출력합니다. 검출 방식은 `Unknown / EdgePeriodicity / FractionalRefinement / Manual`입니다.

## Coarse → fractional refinement

1. 인접 픽셀의 alpha 및 premultiplied RGB 차이를 X/Y 축으로 평균 냅니다. 완전 투명 픽셀의 숨은 RGB는 경계를 만들지 않습니다. 이미지 픽셀 순회는 이때 한 번 수행합니다.
2. 기존 정수 edge-periodicity detector를 그대로 coarse 후보 생성에 사용합니다. 추가로 관측된 경계 피크 사이의 간격과 그 작은 배수 관계에서 후보를 만듭니다. 따라서 정수 detector가 31이나 48 같은 잘못된 배수를 골라도 18.x 후보를 함께 평가할 수 있습니다.
3. 실수 경로는 낮은 분위수로 잡음 바닥을 차감하고, 국소 피크의 에너지 중심으로 subpixel 경계 위치를 추정합니다. 큰 피크의 영향은 상위 분위수로 제한합니다. 한 source pixel에 집중된 날카로운 경계는 물리적 위치에 최대 ±0.5px의 raster 양자화 불확실성이 있음을 반영합니다.
4. 기본적으로 coarse 후보 주변 ±1.5px를 0.1px 간격으로 탐색하고 오프셋은 [0, period)에서 0.5px 간격으로 평가합니다. 서로 다른 상위 후보 5개 주변 ±0.15px를 0.01px 간격으로 정제하며 오프셋은 0.05px 간격으로 세분화합니다.
5. 관측 경계와 lattice 인덱스의 가중 선형 적합을 추가 후보로 평가합니다. 개선된 동일 점수를 얻을 때만 채택합니다. 정확한 정수 후보도 같은 목적 함수로 비교하며 정수에 가산점을 주지 않습니다.

점수는 관측 경계의 lattice 정렬/설명률, 보간한 boundary edge energy, 관측 경계 밀도, 셀 중앙의 edge penalty, 네 공간 구간의 위상 일관성을 결합합니다. 선형 보간으로 실수 좌표의 신호를 읽습니다. 모든 논리 경계가 보여야 한다고 요구하지 않으며 관측 가능한 경계가 최소 3개 있어야 합니다.

원점 근처의 작은 음수 위상이 period 끝으로 정규화되거나, 작은 추정 오차로 마지막 셀이 사라질 수 있습니다. 이런 경우에만 최대 0.5px의 raster 위치 불확실성 안에서 이미지 시작/끝에 맞춘 후보를 추가 비교합니다. 지지 경계 수를 유지하고 점수 차이가 0.2% 이내일 때 채택합니다. 경계가 대부분 단일 source pixel에 집중된 NN형 입력에서는 subpixel 위상을 정확히 알 수 없어 최대 4%의 점수 차이를 허용합니다. 보정 여부는 진단 정보에 표시합니다. 이미지 크기의 약수를 전수 조사해서 주기를 정하지 않습니다.

Confidence는 경계 점수, 지지 경계 수, 전체 이미지에서의 공간 범위, 위상/셀 내부 일관성, 다른 주기와의 분리도를 합친 0~1 **상대 품질 지표**입니다. 통계적인 정답 확률이 아닙니다. 정보가 없는 축은 안전한 기본 크기와 0 Confidence를 반환하며 전체 Confidence는 두 축 중 낮은 값입니다.

`GridDetectionOptions`에서 기본 2~64px 검색 범위, refinement 범위/간격, offset 간격, 경계 정렬 허용 폭, coarse 후보 수를 설정할 수 있습니다. 후보 점수는 재사용한 1차원 신호와 경계 목록으로 계산합니다. `O(imagePixels × 후보 수)` 구조나 외부 numerical library를 사용하지 않습니다.

## Fractional cell recovery

`GridSampler`는 원본에서 다음 영역을 직접 읽습니다:

```text
left   = OffsetX + column       * CellWidth
right  = OffsetX + (column + 1) * CellWidth
top    = OffsetY + row          * CellHeight
bottom = OffsetY + (row + 1)    * CellHeight
```

source pixel을 [x,x+1) × [y,y+1)로 보고, 실수 셀과의 겹친 가로·세로 길이를 곱한 면적을 가중치로 씁니다. 중간 resize나 정규화 이미지가 최종 출력에 개입하지 않습니다.

완전히 포함되는 셀 수는 `floor((extent - offset) / period)`입니다. 계산 과정의 부동소수점 오차만 허용하는 extent × 1e-9의 작은 epsilon을 사용하고 마지막 source 범위를 clamp합니다. 검출 오차를 이 epsilon으로 숨기지 않습니다. 셀 크기는 최소 1px, 오프셋은 [0, cell size)이며 NaN/Infinity·음수·빈 결과를 거절합니다.

| 모드 | 대표색 규칙 |
| --- | --- |
| DominantColor (기본) | RGB 각 5비트, alpha 4비트의 구간별 겹친 면적을 합산합니다. 가장 큰 구간에서 면적 가중 중심에 가장 가까운 **실제 source RGBA**를 선택합니다. 동률은 고정된 색상 키 순서로 결정합니다. |
| AreaWeightedAverage | RGB = Σ(area × alpha × channel) / Σ(area × alpha). Alpha = Σ(area × alpha) / Σ(area). |
| Average | 실수 경로에서는 AreaWeightedAverage와 동일합니다. 기존 이름을 유지합니다. |
| Median | 면적 가중 R/G/B 중앙값은 A > 0인 source pixel만 사용합니다. Alpha 중앙값에는 투명 픽셀도 포함합니다. 정확히 절반인 경우 다음 값과 평균합니다. |
| Center | 실수 셀 중심을 포함하는 source pixel을 선택합니다. 정수 짝수 셀에서는 기존처럼 중앙 네 픽셀 중 오른쪽 아래입니다. |

DominantColor는 전체 색 공간에 대한 medoid 최적화가 아니라 작은 고정 histogram 구간을 사용하는 결정론적 대표색입니다. 경계의 혼합색보다 내부의 실제 색이 우세한 픽셀아트에 적합합니다. source에 있던 antialiasing 혼합색 자체가 우세하면 그것이 선택될 수 있습니다. 전체 또는 최종 alpha가 0인 색의 RGB는 0으로 정규화합니다.

모든 연산은 8비트 채널 공간에서 수행합니다. 평균·짝수 중앙값은 가장 가까운 정수로 반올림하고 절반이면 올립니다. 원본은 수정하지 않습니다. 파일을 읽은 뒤 핸들을 해제하고, 저장은 임시 PNG 인코딩을 완료한 후 대상 파일을 교체합니다.

## 주요 파일

```text
src/PixelGridRecovery.Core/
  GridInfo.cs                   기존 정수 모델
  GridGeometry.cs               double geometry, 영역, 결과와 축별 diagnostics
  EdgeSignals.cs                공통 X/Y 신호 생성
  GridDetector.cs                기존 정수 검출 + 실수 경로 진입점
  FractionalPeriodRefiner.cs     후보 생성, 보간 점수, period/offset 정제
  GridDetectionOptions.cs        검색 설정
  GridCropper.cs / BlockReducer.cs   정수 API 보존
  GridSampler.cs                면적 기반 원본 직접 복원
  ImageProcessingService.cs     정수/실수 서비스 API
src/PixelGridRecovery.App/
  MainForm.cs / MainForm.Layout.cs   실수 숫자 입력과 기존 workflow
  ImagePreviewControl.cs        double geometry 오버레이
  BitmapCodec.cs                System.Drawing PNG/JPG 입출력
tests/PixelGridRecovery.Tests/
  FractionalSyntheticImages.cs   독립적인 logical-coordinate NN/면적 rasterizer
  FractionalDetectionTests.cs    배율·offset·drift·배경·노이즈·결정론
  GridSamplerTests.cs            면적, alpha, 경계, 정수 회귀
tests/PixelGridRecovery.App.Tests/
  BitmapCodecTests.cs            수동/자동 fractional geometry PNG 왕복
  MainFormLayoutTests.cs         폼 렌더링과 소수점 입력 설정
```

기존 정수 모델/검출/크롭/대표색 테스트도 그대로 실행합니다.

## 테스트와 확인 범위

- 단순 random-color fixture 외에 얼굴·눈·입·외곽선·긴 같은 색 구간을 가진 64×64 논리 스프라이트를 사용합니다.
- 정수 2/4/8/18/19배에서 정확한 정수 geometry와 전체 출력 픽셀을 비교합니다.
- fractional 2.5/4.25/7.5/18.4/18.6/18.75/19.2배의 NN 및 면적 rasterization을 검사합니다.
- 18.6배, offset 7.35/12.7에서 처음/중간/마지막 경계 오차와 전체 복원 스프라이트를 검사합니다. 19px 강제 반올림의 25.6px drift와 구분합니다.
- 원점에 맞는 fractional 입력에서 작은 위상 오차 때문에 행/열 하나를 잃지 않는지 검사합니다.
- ±2/±4 RGB noise, 3×3 약한 blur, 큰 균일 배경, 18.6×17.4 독립 축, 반복 호출의 결정론, 단색의 낮은 confidence를 검사합니다.
- 정확한 source 겹침 면적·alpha, 실제 source 색 선택, 정수 대표색 회귀, floating-point 셀 수, 잘못된 geometry 거절을 검사합니다.
- 수동 18.55×18.70 / offset 7.35,12.7 및 자동 검출 결과를 PNG 저장 후 다시 읽어 논리 픽셀과 비교합니다.
- 테스트 로그에는 검출 시간과 축별 diagnostics, drift 오차가 기록됩니다. 실제 실행 결과는 별도 작업 보고를 참고하세요.

## 한계

- 하나의 이미지에서 축마다 일정한 전역 배율·위상을 가정합니다. 회전, 원근, 국소 변형, 영역별 다른 배율은 처리하지 않습니다.
- 관측 경계들이 원래 격자의 같은 배수 위치에만 있으면 fundamental scale을 유일하게 식별할 수 없습니다. 여러 후보가 비슷하면 수동 조정이 필요합니다.
- NN raster의 subpixel 위상은 source pixel 양자화로 정확히 유일하지 않을 수 있습니다. 이미지 끝과의 차이가 이 불확실성보다 크면 실제 부분 셀로 보고 제거합니다.
- 원점/끝 경계 보정은 동등한 점수의 후보 중 선택하는 규칙입니다. 진짜 아주 작은 crop/offset과 raster 양자화가 구별되지 않는 경우 경계 셀 포함 여부가 달라질 수 있습니다. 수동 geometry에는 이 검출 보정을 적용하지 않습니다.
- 강한 blur·압축·잡음, 작은 2~3px 셀, 충분하지 않은 반복 경계에서는 검출 또는 대표색 복원이 불안정할 수 있습니다. 리샘플링으로 사라진 색과 세부 정보는 만들어 내지 않습니다.
- 후보 수와 검색 범위가 제한된 결정론적 탐색이며 모든 AI 생성 이미지의 최적 lattice를 보장하지 않습니다. 실제 AI 입력은 별도로 검토해야 합니다.
- 입력 상한은 16,777,216 픽셀입니다. 명시적인 버튼 클릭에서 동기 처리하므로 큰 이미지/작은 셀에서는 잠시 기다릴 수 있습니다.
- 8비트 RGBA PNG 출력이며 원본 고비트 심도, EXIF/ICC 메타데이터는 보존하지 않습니다.
- AI/딥러닝, OpenCV, 배경 제거, 정규화 미리보기, 회전/원근/mesh 보정, palette editor, 애니메이션, batch 처리는 포함하지 않습니다.
