namespace PetAnimalFriendshipTweaks
{
    public class ModConfig
    {
        public bool ModEnabled { get; set; } = true;

        // Vanilla cap is 1000 (5 hearts @ 200 pts/heart). 2000 = 10 hearts at the same density.
        public int MaxFriendshipPoints { get; set; } = 1400;
    }
}