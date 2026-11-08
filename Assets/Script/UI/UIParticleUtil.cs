using Coffee.UIExtensions;
using UnityEngine;

// 일반 ParticleSystem 프리팹을 UIParticle로 감싸 UI(Canvas) 안에 띄우는 공용 헬퍼.
// Screen Space - Overlay 캔버스는 일반 파티클보다 항상 위에 그려지므로, UI 위에 이펙트를 보이려면 이렇게 감싸야 한다.
public static class UIParticleUtil
{
    /// <summary>parent 아래에 UIParticle 오브젝트를 만들고 effectPrefab을 넣어 반환 (정지 상태).
    /// 재생은 반환값의 Clear() → Play().</summary>
    /// <param name="scale">파티클 1 유닛을 몇 픽셀로 그릴지 (캔버스 기준 해상도 기준)</param>
    public static UIParticle Create(GameObject effectPrefab, Transform parent, float scale, string name = "UIParticleFX")
    {
        if (effectPrefab == null || parent == null) return null;

        var holder = new GameObject(name, typeof(RectTransform));
        holder.layer = parent.gameObject.layer;
        var rect = (RectTransform)holder.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        var particle = holder.AddComponent<UIParticle>();
        particle.raycastTarget   = false; // 이펙트가 클릭을 가로채지 않도록
        particle.autoScalingMode = UIParticle.AutoScalingMode.UIParticle;
        particle.scale           = scale;

        var fx = Object.Instantiate(effectPrefab);
        // 메뉴가 게임을 멈춘 상태(timeScale 0)에서도 재생되도록 실제 시간 기준으로
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.useUnscaledTime = true;
            main.playOnAwake     = false;
        }
        particle.SetParticleSystemInstance(fx);
        particle.Stop();

        return particle;
    }

    /// <summary>처음부터 다시 재생</summary>
    public static void Replay(UIParticle particle)
    {
        if (particle == null) return;
        particle.transform.SetAsLastSibling(); // 같은 부모의 다른 UI보다 위에
        particle.Clear();
        particle.Play();
    }
}
