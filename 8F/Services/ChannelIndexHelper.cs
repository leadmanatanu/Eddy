namespace _8F.Services
{
    public static class ChannelIndexHelper
    {
        public static int ToDbChannel(int zeroBasedIndex) => zeroBasedIndex + 1;
        public static int ToZeroBasedIndex(int oneBasedChannel) => oneBasedChannel - 1;
    }
}
