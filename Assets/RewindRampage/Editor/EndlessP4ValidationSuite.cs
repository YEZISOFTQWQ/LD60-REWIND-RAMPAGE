namespace RewindRampage.Editor
{
    public static class EndlessP4ValidationSuite
    {
        public static void Run()
        {
            BossCombinedValidation.Run();
            EndlessValidation.Run();
            BossCombinedPreview.Render();
            TapeTransportPreview.RenderBossHud();
        }
    }
}
