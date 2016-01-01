// Created by Ron 'Maxwolf' McDowell (ron.mcdowell@gmail.com) 
// Timestamp 01/01/2016@5:26 AM

namespace WolfCurses.Module
{
    /// <summary>
    ///     The Module interface.
    /// </summary>
    public interface IModule : ITick
    {
        /// <summary>
        ///     Fired when the simulation is closing and needs to clear out any data structures that it created so the program can
        ///     exit cleanly.
        /// </summary>
        void Destroy();
    }
}