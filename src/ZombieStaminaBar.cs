using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RideZombie;

static class ZombieStaminaBar
{
    const float Gap = 8f;
    static readonly Color Fill = new(0.22f, 0.52f, 0.08f);

    static StaminaBar bar;

    public static void Tick()
    {
        var local = Character.localCharacter;
        var zombie = local == null ? null : Rides.MountOf(local);
        if (bar == null && zombie == null)
            return;
        if (bar == null)
            bar = Clone(GUIManager.instance.bar);
        bar.gameObject.SetActive(zombie != null);
        if (zombie == null)
            return;
        var stamina = zombie.data.currentStamina;
        var width = bar.fullBar.sizeDelta.x;
        var desired = Mathf.Max(0f, stamina * width + bar.staminaBarOffset);
        var step = Time.deltaTime * 10f;
        Resize(bar.staminaBar, Mathf.Lerp(bar.staminaBar.sizeDelta.x, desired, step));
        Resize(bar.maxStaminaBar, Mathf.Lerp(bar.maxStaminaBar.sizeDelta.x,
            Mathf.Max(0f, zombie.GetMaxStamina() * width + bar.staminaBarOffset), step));
        Resize(bar.staminaBarOutline, 14f + width);
        bar.staminaBar.gameObject.SetActive(bar.staminaBar.sizeDelta.x > bar.minStaminaBarWidth);
        bar.maxStaminaBar.gameObject.SetActive(bar.maxStaminaBar.sizeDelta.x > bar.minStaminaBarWidth);

        var trailing = Mathf.Clamp01((bar.staminaBar.sizeDelta.x - desired) * 0.5f);
        bar.sinTime = (bar.sinTime + step * trailing) % (Mathf.PI * 2f);
        var glow = bar.staminaGlow.color;
        glow.a = trailing * 0.4f - Mathf.Abs(Mathf.Sin(bar.sinTime)) * 0.2f;
        bar.staminaGlow.color = glow;

        if (stamina <= 0.005f && !bar.outOfStamina)
            bar.OutOfStaminaPulse();
        bar.outOfStamina = stamina <= 0.005f;
    }

    static StaminaBar Clone(StaminaBar source)
    {
        var clone = Object.Instantiate(source.gameObject, source.transform.parent).GetComponent<StaminaBar>();
        clone.enabled = false;
        clone.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var afflictions = clone.GetComponentsInChildren<BarAffliction>(true);
        foreach (var affliction in afflictions)
            affliction.gameObject.SetActive(false);
        foreach (var extra in new Component[]
                 {
                     clone.extraBar, clone.shield.transform, clone.campfire.transform, clone.moraleBoostText,
                     clone.rainbowStamina, clone.staminaBarOutlineOverflowBar,
                 })
            extra.gameObject.SetActive(false);

        var spores = afflictions.First(a => a.afflictionType == CharacterAfflictions.STATUSTYPE.Spores).transform;
        var pattern = spores.Find("Fill").GetComponent<Image>();
        var fill = clone.staminaBar.Find("Fill").GetComponent<Image>();
        fill.sprite = pattern.sprite;
        fill.material = pattern.material;
        fill.type = pattern.type;
        fill.pixelsPerUnitMultiplier = pattern.pixelsPerUnitMultiplier;
        fill.color = Fill;
        var outline = (RectTransform)Object.Instantiate(spores.Find("Outline").gameObject, fill.transform, false).transform;
        outline.sizeDelta = new Vector2(6f, 8f);
        outline.GetComponent<Image>().color = Fill;

        ((RectTransform)clone.transform).anchoredPosition += Vector2.up * (clone.staminaBarOutline.sizeDelta.y + Gap);
        return clone;
    }

    static void Resize(RectTransform rect, float width) => rect.sizeDelta = new Vector2(Mathf.Max(0f, width), rect.sizeDelta.y);
}
