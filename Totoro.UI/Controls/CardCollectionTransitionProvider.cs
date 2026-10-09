using System.Numerics;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;

namespace Totoro.UI.Controls;

/// <summary>
/// Animates <see cref="ItemsRepeater"/> collection changes in three phases:
/// removed cards fade out, cards that stay slide to their new position, then added cards fly in, staggered.
/// Plain re-layouts (e.g. resizing the window) slide the cards to their new position too; see <see cref="LayoutMoveDuration"/>.
/// </summary>
public partial class CardCollectionTransitionProvider : ItemCollectionTransitionProvider
{
	public TimeSpan RemoveDuration { get; set; } = TimeSpan.FromMilliseconds(150);
	public TimeSpan MoveDuration { get; set; } = TimeSpan.FromMilliseconds(400);

	/// <summary>
	/// Duration of a move caused only by a re-layout (e.g. resizing the window) when the card shifts by one card width.
	/// Longer moves, like a card wrapping from the start of a row to the end of the previous one, take longer
	/// (growing with the square root of the distance) so they don't streak across the screen.
	/// </summary>
	public TimeSpan LayoutMoveDuration { get; set; } = TimeSpan.FromMilliseconds(280);

	/// <summary>Upper bound for <see cref="LayoutMoveDuration"/> after it is scaled by distance.</summary>
	public TimeSpan MaxLayoutMoveDuration { get; set; } = TimeSpan.FromMilliseconds(600);
	public TimeSpan AddDuration { get; set; } = TimeSpan.FromMilliseconds(400);

	/// <summary>Delay between consecutive added cards.</summary>
	public TimeSpan Stagger { get; set; } = TimeSpan.FromMilliseconds(30);

	/// <summary>Caps the total stagger so long lists don't take ages to fill in.</summary>
	public int MaxStaggeredItems { get; set; } = 12;

	/// <summary>How far below its final position an added card starts.</summary>
	public float AddOffset { get; set; } = 40;

	protected override bool ShouldAnimateCore(ItemCollectionTransition transition) => true;

	protected override void StartTransitions(IList<ItemCollectionTransition> transitions)
	{
		var removes = transitions.Where(x => x.Operation == ItemCollectionTransitionOperation.Remove).ToList();
		var moves = transitions.Where(x => x.Operation == ItemCollectionTransitionOperation.Move).ToList();
		var adds = transitions.Where(x => x.Operation == ItemCollectionTransitionOperation.Add).ToList();

		// Each phase starts once the one before it has made room.
		var moveDelay = removes.Count > 0 ? RemoveDuration : TimeSpan.Zero;
		var addDelay = moveDelay + (moves.Count > 0 ? MoveDuration / 2 : TimeSpan.Zero);

		foreach (var transition in removes)
		{
			StartRemove(transition);
		}

		foreach (var transition in moves)
		{
			StartMove(transition, moveDelay);
		}

		for (var i = 0; i < adds.Count; i++)
		{
			StartAdd(adds[i], addDelay + Stagger * Math.Min(i, MaxStaggeredItems));
		}
	}

	private void StartRemove(ItemCollectionTransition transition)
	{
		var progress = transition.Start();
		var element = progress.Element;

		AnimationBuilder.Create()
			.Opacity(to: 0, duration: RemoveDuration, easingMode: EasingMode.EaseIn)
			.Scale(to: 0.9, duration: RemoveDuration, easingMode: EasingMode.EaseIn)
			.Start(element, () =>
			{
				progress.Complete();

				// The repeater recycles this element for another item, so undo the fade-out.
				var visual = ElementCompositionPreview.GetElementVisual(element);
				visual.Opacity = 1;
				visual.Scale = Vector3.One;
			});
	}

	private void StartMove(ItemCollectionTransition transition, TimeSpan delay)
	{
		var progress = transition.Start();
		var element = progress.Element;

		// The element is already arranged at NewBounds; start it back where it was and slide it over.
		var from = new Vector3(
			(float)(transition.OldBounds.X - transition.NewBounds.X),
			(float)(transition.OldBounds.Y - transition.NewBounds.Y),
			0);

		SetTranslation(element, from);

		var builder = transition.Triggers == ItemCollectionTransitionTriggers.LayoutTransition
			// Resizing restarts these on every layout pass, so ease out (an ease-in would stall at each restart),
			// but with Sine rather than Cubic so the start isn't a sudden jump.
			? AnimationBuilder.Create().Translation(to: Vector3.Zero, from: from, delay: delay, duration: GetLayoutMoveDuration(transition, from),
				easingType: EasingType.Sine, easingMode: EasingMode.EaseOut)
			: AnimationBuilder.Create().Translation(to: Vector3.Zero, from: from, delay: delay, duration: MoveDuration,
				easingType: EasingType.Cubic, easingMode: EasingMode.EaseInOut);

		builder.Start(element, progress.Complete);
	}

	private TimeSpan GetLayoutMoveDuration(ItemCollectionTransition transition, Vector3 offset)
	{
		var cardWidth = Math.Max(transition.NewBounds.Width, 1);
		var hops = Math.Max(offset.Length() / cardWidth, 1);
		var duration = LayoutMoveDuration * Math.Sqrt(hops);

		return duration < MaxLayoutMoveDuration ? duration : MaxLayoutMoveDuration;
	}

	private void StartAdd(ItemCollectionTransition transition, TimeSpan delay)
	{
		var progress = transition.Start();
		var element = progress.Element;
		var from = new Vector3(0, AddOffset, 0);

		// Hide it right away, otherwise it shows at its final position until the delay runs out.
		var visual = ElementCompositionPreview.GetElementVisual(element);
		visual.Opacity = 0;
		SetTranslation(element, from);

		AnimationBuilder.Create()
			.Opacity(to: 1, from: 0, delay: delay, duration: AddDuration, easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
			.Translation(to: Vector3.Zero, from: from, delay: delay, duration: AddDuration, easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
			.Scale(to: 1, from: 0.94, delay: delay, duration: AddDuration, easingType: EasingType.Cubic, easingMode: EasingMode.EaseOut)
			.Start(element, progress.Complete);
	}

	private static void SetTranslation(UIElement element, Vector3 translation)
	{
		ElementCompositionPreview.SetIsTranslationEnabled(element, true);
		ElementCompositionPreview.GetElementVisual(element).Properties.InsertVector3("Translation", translation);
	}
}
