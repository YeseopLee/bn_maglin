using UnityEngine;
using System.Collections.Generic;

namespace Maglin.Battle
{
    /// <summary>
    /// 스프라이트 텍스처를 실제로 조각내어 분할하는 클래스
    /// </summary>
    public static class SpriteTextureFragmenter
    {
        /// <summary>
        /// 스프라이트를 실제 조각들로 분할
        /// </summary>
        public static List<Sprite> FragmentSprite(Sprite originalSprite, int fragmentCount)
        {
            List<Sprite> fragments = new List<Sprite>();

            if (originalSprite == null || originalSprite.texture == null)
            {
                Debug.LogWarning("[SpriteTextureFragmenter] 원본 스프라이트나 텍스처가 null입니다.");
                return fragments;
            }

            // 조각 그리드 계산
            int cols = Mathf.CeilToInt(Mathf.Sqrt(fragmentCount));
            int rows = Mathf.CeilToInt((float)fragmentCount / cols);

            // 원본 텍스처 정보
            Texture2D originalTexture = originalSprite.texture;
            Rect spriteRect = originalSprite.rect;

            // 텍스처를 읽을 수 있도록 임시 텍스처 생성
            Texture2D readableTexture = CreateReadableTexture(originalTexture, spriteRect);

            if (readableTexture == null)
            {
                Debug.LogWarning("[SpriteTextureFragmenter] 읽기 가능한 텍스처를 생성할 수 없습니다.");
                return fragments;
            }

            // 조각 크기 계산
            int fragmentWidth = Mathf.CeilToInt(readableTexture.width / (float)cols);
            int fragmentHeight = Mathf.CeilToInt(readableTexture.height / (float)rows);

            for (int i = 0; i < fragmentCount; i++)
            {
                int col = i % cols;
                int row = i / cols;

                if (row >= rows) break;

                // 조각 텍스처 생성
                Sprite fragmentSprite = CreateFragmentSprite(readableTexture, col, row, fragmentWidth, fragmentHeight, originalSprite.pixelsPerUnit, i);
                
                if (fragmentSprite != null)
                {
                    fragments.Add(fragmentSprite);
                }
            }

            // 임시 텍스처 정리 (원본이 아닌 경우만)
            if (readableTexture != originalTexture)
            {
                Object.DestroyImmediate(readableTexture);
            }

            Debug.Log($"[SpriteTextureFragmenter] {fragments.Count}개의 조각 스프라이트 생성됨");
            return fragments;
        }

        /// <summary>
        /// 읽기 가능한 텍스처 생성
        /// </summary>
        private static Texture2D CreateReadableTexture(Texture2D originalTexture, Rect spriteRect)
        {
            // 텍스처가 이미 읽기 가능한 경우
            if (originalTexture.isReadable)
            {
                return CreateCroppedTexture(originalTexture, spriteRect);
            }

            // 읽기 불가능한 텍스처인 경우 RenderTexture를 사용하여 복사
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                Mathf.FloorToInt(spriteRect.width),
                Mathf.FloorToInt(spriteRect.height),
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );

            // 원본 텍스처를 RenderTexture에 그리기
            Graphics.Blit(originalTexture, renderTexture);

            // RenderTexture에서 읽기 가능한 Texture2D로 변환
            RenderTexture.active = renderTexture;
            Texture2D readableTexture = new Texture2D(
                Mathf.FloorToInt(spriteRect.width),
                Mathf.FloorToInt(spriteRect.height),
                TextureFormat.ARGB32,
                false
            );

            // 스프라이트 영역만 읽기
            readableTexture.ReadPixels(new Rect(0, 0, spriteRect.width, spriteRect.height), 0, 0);
            readableTexture.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(renderTexture);

            return readableTexture;
        }

        /// <summary>
        /// 스프라이트 영역만 잘라낸 텍스처 생성
        /// </summary>
        private static Texture2D CreateCroppedTexture(Texture2D originalTexture, Rect spriteRect)
        {
            int x = Mathf.FloorToInt(spriteRect.x);
            int y = Mathf.FloorToInt(spriteRect.y);
            int width = Mathf.FloorToInt(spriteRect.width);
            int height = Mathf.FloorToInt(spriteRect.height);

            // 범위 검증
            x = Mathf.Clamp(x, 0, originalTexture.width);
            y = Mathf.Clamp(y, 0, originalTexture.height);
            width = Mathf.Clamp(width, 1, originalTexture.width - x);
            height = Mathf.Clamp(height, 1, originalTexture.height - y);

            Color[] pixels = originalTexture.GetPixels(x, y, width, height);
            
            Texture2D croppedTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();

            return croppedTexture;
        }

        /// <summary>
        /// 개별 조각 스프라이트 생성
        /// </summary>
        private static Sprite CreateFragmentSprite(Texture2D sourceTexture, int col, int row, int fragmentWidth, int fragmentHeight, float pixelsPerUnit, int fragmentIndex)
        {
            // 조각 위치 계산
            int startX = col * fragmentWidth;
            int startY = row * fragmentHeight;

            // 범위 검증 및 조정
            int actualWidth = Mathf.Min(fragmentWidth, sourceTexture.width - startX);
            int actualHeight = Mathf.Min(fragmentHeight, sourceTexture.height - startY);

            if (actualWidth <= 0 || actualHeight <= 0)
            {
                return null;
            }

            // 조각 픽셀 추출
            Color[] fragmentPixels = sourceTexture.GetPixels(startX, startY, actualWidth, actualHeight);

            // 투명한 조각도 포함 - 설정한 조각 수만큼 정확히 생성
            // (투명한 조각은 시각적으로 보이지 않지만 애니메이션에는 참여)
            int visiblePixelCount = 0;
            for (int i = 0; i < fragmentPixels.Length; i++)
            {
                if (fragmentPixels[i].a > 0.001f)
                {
                    visiblePixelCount++;
                }
            }

            // 만약 완전히 투명하다면 최소한의 투명도라도 부여
            if (visiblePixelCount == 0)
            {
                // 완전히 투명한 조각에는 매우 낮은 투명도의 흰색 픽셀 하나 추가
                fragmentPixels[fragmentPixels.Length / 2] = new Color(1f, 1f, 1f, 0.02f);
            }

            // 조각 텍스처 생성
            Texture2D fragmentTexture = new Texture2D(actualWidth, actualHeight, TextureFormat.ARGB32, false);
            fragmentTexture.SetPixels(fragmentPixels);
            fragmentTexture.Apply();

            // 조각 스프라이트 생성
            Sprite fragmentSprite = Sprite.Create(
                fragmentTexture,
                new Rect(0, 0, actualWidth, actualHeight),
                new Vector2(0.5f, 0.5f), // 중앙 pivot
                pixelsPerUnit
            );

            fragmentSprite.name = $"Fragment_{fragmentIndex}_{col}_{row}";

            return fragmentSprite;
        }

        /// <summary>
        /// 향상된 조각 생성 - 불규칙한 조각 모양 지원
        /// </summary>
        public static List<Sprite> FragmentSpriteIrregular(Sprite originalSprite, int fragmentCount, float irregularityFactor = 0.3f)
        {
            List<Sprite> fragments = new List<Sprite>();

            if (originalSprite == null || originalSprite.texture == null)
            {
                return fragments;
            }

            // 기본 조각화 실행
            List<Sprite> regularFragments = FragmentSprite(originalSprite, fragmentCount);

            // 불규칙성 적용 (향후 확장용)
            foreach (var fragment in regularFragments)
            {
                if (fragment != null)
                {
                    fragments.Add(fragment);
                }
            }

            return fragments;
        }

        /// <summary>
        /// 조각 스프라이트들을 GameObjects로 생성
        /// </summary>
        public static List<GameObject> CreateFragmentGameObjects(List<Sprite> fragmentSprites, Vector3 centerPosition, Vector3 scale, Color color)
        {
            List<GameObject> fragmentObjects = new List<GameObject>();

            if (fragmentSprites == null || fragmentSprites.Count == 0)
            {
                return fragmentObjects;
            }

            // 조각 그리드 계산
            int cols = Mathf.CeilToInt(Mathf.Sqrt(fragmentSprites.Count));
            int rows = Mathf.CeilToInt((float)fragmentSprites.Count / cols);

            // 전체 크기 계산 (원본 스프라이트 기준)
            float totalWidth = scale.x;
            float totalHeight = scale.y;
            float fragmentWidth = totalWidth / cols;
            float fragmentHeight = totalHeight / rows;

            for (int i = 0; i < fragmentSprites.Count; i++)
            {
                Sprite fragmentSprite = fragmentSprites[i];
                if (fragmentSprite == null) continue;

                int col = i % cols;
                int row = i / cols;

                // 조각 위치 계산
                float x = centerPosition.x - totalWidth * 0.5f + fragmentWidth * (col + 0.5f);
                float y = centerPosition.y - totalHeight * 0.5f + fragmentHeight * (row + 0.5f);
                Vector3 fragmentPosition = new Vector3(x, y, centerPosition.z);

                // 조각 게임오브젝트 생성
                GameObject fragmentObj = new GameObject($"SpriteFragment_{i}_{col}_{row}");
                fragmentObj.transform.position = fragmentPosition;
                fragmentObj.transform.localScale = scale;

                // SpriteRenderer 설정
                SpriteRenderer renderer = fragmentObj.AddComponent<SpriteRenderer>();
                renderer.sprite = fragmentSprite;
                renderer.color = color;
                renderer.sortingOrder = 100;

                fragmentObjects.Add(fragmentObj);
            }

            Debug.Log($"[SpriteTextureFragmenter] {fragmentObjects.Count}개의 조각 게임오브젝트 생성됨");
            return fragmentObjects;
        }

        /// <summary>
        /// 조각 스프라이트들 정리 (메모리 해제)
        /// </summary>
        public static void CleanupFragmentSprites(List<Sprite> fragmentSprites)
        {
            if (fragmentSprites == null) return;

            foreach (var sprite in fragmentSprites)
            {
                if (sprite != null && sprite.texture != null)
                {
                    Object.DestroyImmediate(sprite.texture);
                    Object.DestroyImmediate(sprite);
                }
            }

            fragmentSprites.Clear();
        }
    }
} 