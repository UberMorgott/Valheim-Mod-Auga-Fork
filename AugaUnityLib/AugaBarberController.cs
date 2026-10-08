using UnityEngine;
using UnityEngine.UI;

namespace AugaUnity
{
    public class AugaBarberController : MonoBehaviour
    {
        public Button HairRightButton;
        public Button HairLeftButton;
        public Button BeardRightButton;
        public Button BeardLeftButton;

        private PlayerCustomizaton _playerCustomization;
        private void Awake()
        {
            _playerCustomization = GetComponent<PlayerCustomizaton>();
        }

        private void Start()
        {
            _playerCustomization.m_beards = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Beard");
            _playerCustomization.m_hairs = ObjectDB.instance.GetAllItems(ItemDrop.ItemData.ItemType.Customization, "Hair");
            _playerCustomization.m_beards.RemoveAll(x => x.name.Contains("_"));
            _playerCustomization.m_hairs.RemoveAll(x => x.name.Contains("_"));
            _playerCustomization.m_beards.Sort((x, y) => Localization.instance.Localize(x.m_itemData.m_shared.m_name).CompareTo(Localization.instance.Localize(y.m_itemData.m_shared.m_name)));
            _playerCustomization.m_hairs.Sort((x, y) => Localization.instance.Localize(x.m_itemData.m_shared.m_name).CompareTo(Localization.instance.Localize(y.m_itemData.m_shared.m_name)));
        }

        private void Update()
        {
            // Same gate as vanilla PlayerCustomizaton.Update (PlayerCustomizaton.cs:103, game 1.0.16):
            // work only while the barber panel is open and a player exists. The controller lives on
            // the always-active BarberGui object, so without this it ran every frame, and after logout
            // (Player.m_localPlayer destroyed, no FejdStartup parent) GetPlayer() is null -> NRE.
            var customization = _playerCustomization;
            if (customization.m_hairs == null || customization.m_beards == null
                || (customization.m_rootPanel && !customization.m_rootPanel.activeInHierarchy)
                || customization.GetPlayer() == null)
                return;

            var hairSetting = customization.GetHairIndex();
            var beardSetting = customization.GetBeardIndex();

            // Set both arrows every frame: hiding only one let a jump between the ends leave both hidden.
            HairLeftButton.gameObject.SetActive(hairSetting > 0);
            HairRightButton.gameObject.SetActive(hairSetting < customization.m_hairs.Count - 1);
            BeardLeftButton.gameObject.SetActive(beardSetting > 0);
            BeardRightButton.gameObject.SetActive(beardSetting < customization.m_beards.Count - 1);
        }
    }
}
