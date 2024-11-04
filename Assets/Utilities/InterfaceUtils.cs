using DG.Tweening;
using Steamworks;
using System.Threading.Tasks;
using UnityEngine;

using UIImage = UnityEngine.UI.Image;
using SteamImage = Steamworks.Data.Image;

namespace Populous
{
    /// <summary>
    /// The <c>InterfaceUtils</c> class contains utility methods for modifying or animating UI elements.
    /// </summary>
    public static class InterfaceUtils
    {
        /// <summary>
        /// Animates an image to flash red.
        /// </summary>
        /// <param name="image">Image to be animated</param>
        public static void FlashWrong(UIImage image)
            => image.DOColor(new Color(255, 0, 0, 145), 0.5f).SetLoops(2, LoopType.Yoyo);

        /// <summary>
        /// Smoothly transitions the color of an image from it's current color to a new color.
        /// </summary>
        /// <param name="image">The image that should be recolored.</param>
        /// <param name="newColor">The final color of the image.</param>
        /// <param name="duration">The time until the final color is reached.</param>
        public static void SwitchColor(UIImage image, Color newColor, float duration = 0.25f)
            => image.DOColor(newColor, duration);

        /// <summary>
        /// Fades the opacity of the canvas to full opacity.
        /// </summary>
        /// <param name="canvas">The canvas to be faded.</param>
        /// <param name="duration">The time until full opacity.</param>
        public static void FadeMenuIn(CanvasGroup canvas, float duration = 1f)
            => canvas.DOFade(1, duration).OnComplete(() => canvas.interactable = true);

        /// <summary>
        /// Fades the opacity of the canvas to full transparency.
        /// </summary>
        /// <param name="canvas">The canvas to be faded.</param>
        /// <param name="duration">The time until full transparency.</param>
        public static void FadeMenuOut(CanvasGroup canvas, float duration = 1f)
            => canvas.DOFade(0, duration).OnComplete(() => canvas.interactable = false);

        /// <summary>
        /// Converts the Steam Avatar of the given player into a <c>Texture2D</c>.
        /// </summary>
        /// <remarks>Code Snippet from <see href="https://wiki.facepunch.com/steamworks/GetClientAvatarUnity">Facepunch.Steamworks Wiki - Getting A Client's Avatar</see></remarks>
        /// <param name="playerId">The SteamID of the player whose avatar should be converted.</param>
        /// <returns>A task containing the <c>Texture2D</c> of the player's Steam avatar.</returns>
        public static async Task<Texture2D> GetSteamAvatar(SteamId playerId)
        {
            var avatar = await SteamFriends.GetLargeAvatarAsync(playerId);

            if (!avatar.HasValue)
                return null;

            SteamImage image = avatar.Value;

            // Create a new Texture2D
            var texture = new Texture2D((int)image.Width, (int)image.Height, TextureFormat.ARGB32, false)
            {
                // Set filter type, or else its really blury
                filterMode = FilterMode.Trilinear
            };

            // Flip image
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    var p = image.GetPixel(x, y);
                    texture.SetPixel(x, (int)image.Height - y, new Color(p.r / 255.0f, p.g / 255.0f, p.b / 255.0f, p.a / 255.0f));
                }
            }

            texture.Apply();

            return texture;
        }
    }
}