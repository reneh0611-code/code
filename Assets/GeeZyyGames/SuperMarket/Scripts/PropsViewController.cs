using UnityEngine;

namespace GeeZyyGames
{
    public class PropsViewController : MonoBehaviour
    {
        public float rotateSpeed;

        [Header("Assign all character GameObjects here")]
        public GameObject[] characters;

        [Header("Current Character")]
        public int currentIndex = 0;

        public Transform cam;
        public float lerpSpeed;

        private void Start()
        {
            ShowCharacter(currentIndex);
        }

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
            if (currentIndex == 1 || currentIndex == 4)
            {
                CamZoom(-5);
            }
            else if (currentIndex == 5)
            {
                CamZoom(-28);
            }
            else if (currentIndex == 3)
            {
                CamZoom(-10);
            }
            else if (currentIndex == 6)
            {
                CamZoom(-7);
            }
            else
            {
                CamZoom(-15);
            }
        }

        public void NextCharacter()
        {
            if (characters.Length == 0) return;

            currentIndex++;

            if (currentIndex >= characters.Length)
                currentIndex = 0; // Loop back to first

            ShowCharacter(currentIndex);
        }

        public void PreviousCharacter()
        {
            if (characters.Length == 0) return;

            currentIndex--;

            if (currentIndex < 0)
                currentIndex = characters.Length - 1; // Loop to last

            ShowCharacter(currentIndex);
        }

        private void ShowCharacter(int index)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                characters[i].SetActive(i == index);
            }
        }

        public void CamZoom(int val)
        {
            cam.transform.position = Vector3.Lerp(cam.position, new Vector3(cam.position.x, cam.position.y, val), Time.deltaTime * lerpSpeed);
        }
    }
}
