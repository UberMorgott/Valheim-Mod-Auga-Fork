using UnityEngine;
using UnityEngine.UI;

namespace AugaUnity
{
    public class AugaBarberController : MonoBehaviour
    {
        public static AugaBarberController Instance => _instance;
        public Button HairRightButton;
        public Button HairLeftButton;
        public Button BeardRightButton;
        public Button BeardLeftButton;

        private bool _playerFound;
        private static AugaBarberController _instance = null;

        private PlayerCustomizaton _playerCustomization;
        private void Awake()
        {
            _instance = this;
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
            if (!_playerFound)
            {
                if (Player.m_localPlayer == null)
                    return;
                _playerFound = true;
            }

            var hairSetting = _playerCustomization.GetHairIndex();
            var beardSetting = +_playerCustomization.GetBeardIndex();

            if (hairSetting == 0)
                HairLeftButton.gameObject.SetActive(false);
            else if (hairSetting >= (_playerCustomization.m_hairs.Count - 1))
                HairRightButton.gameObject.SetActive(false);
            else
            {
                HairLeftButton.gameObject.SetActive(true);
                HairRightButton.gameObject.SetActive(true);
            }

            if (beardSetting == 0)
                BeardLeftButton.gameObject.SetActive(false);
            else if (beardSetting >= (_playerCustomization.m_beards.Count - 1))
                BeardRightButton.gameObject.SetActive(false);
            else
            {
                BeardLeftButton.gameObject.SetActive(true);
                BeardRightButton.gameObject.SetActive(true);
            }
        }

        public void ResetLocalPlayer()
        {
            _playerFound = false;
        }
    }
}