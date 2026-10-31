// Keep serialized values 0..7 stable for existing assets.
public enum ActionButtonId
{
    Observe = 0, Place = 1, Combine = 2, Lift = 3,
    InsertFrame = 4, TakeFrame = 5, PlaceTopAttachment = 6, Back = 7,
    NewGame = 8, UnlockLand = 9, InventorySlot = 10,
    ObservationPlace = 11, ObservationCombine = 12, ObservationLift = 13,
    TakeTopAttachment = 14, ContinueGame = 15, SaveGame = 16, ConfirmNewGame = 17, CancelNewGame = 18
}

public enum ActionButtonPlacement { Fixed = 0, FollowTarget = 1, Relative = 2, Grid = 3 }
