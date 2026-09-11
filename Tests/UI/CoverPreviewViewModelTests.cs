using System;
using System.Collections.Generic;
using PluginCoverShuffle.Domain;
using PluginCoverShuffle.UI;
using Xunit;

namespace PluginCoverShuffle.Tests.UI
{
    public class CoverPreviewViewModelTests
    {
        private static CoverDisplayItem MakeCover(Guid coverId, bool isCurrent = false, CoverSource source = CoverSource.LocalFile)
        {
            return new CoverDisplayItem
            {
                CoverId = coverId,
                AbsoluteImagePath = null, // Deliberately no real file - these tests only exercise navigation/state, not disk I/O.
                Source = source,
                AddedAt = DateTime.UtcNow,
                IsCurrent = isCurrent,
                IsCoverEnabled = true,
                CoverNumber = 1
            };
        }

        [Fact]
        public void SingleCoverConstructor_HasNoNavigationAndCannotSetAsCurrent()
        {
            var cover = MakeCover(Guid.NewGuid());
            var viewModel = new CoverPreviewViewModel("Some Game", cover);

            Assert.False(viewModel.CanNavigate);
            Assert.False(viewModel.CanMovePrevious);
            Assert.False(viewModel.CanMoveNext);
            Assert.False(viewModel.CanSetAsCurrent);
            Assert.Equal("Cover 1 of 1", viewModel.PositionText);
        }

        [Fact]
        public void MultiCoverConstructor_StartsAtTheRequestedIndex()
        {
            var covers = new List<CoverDisplayItem> { MakeCover(Guid.NewGuid()), MakeCover(Guid.NewGuid()), MakeCover(Guid.NewGuid()) };

            var viewModel = new CoverPreviewViewModel("Some Game", covers, 1, null);

            Assert.Same(covers[1], viewModel.Cover);
            Assert.Equal("Cover 2 of 3", viewModel.PositionText);
            Assert.True(viewModel.CanMovePrevious);
            Assert.True(viewModel.CanMoveNext);
        }

        [Fact]
        public void StartIndex_OutOfRange_IsClampedRatherThanThrowing()
        {
            var covers = new List<CoverDisplayItem> { MakeCover(Guid.NewGuid()), MakeCover(Guid.NewGuid()) };

            var tooHigh = new CoverPreviewViewModel("Some Game", covers, 99, null);
            var tooLow = new CoverPreviewViewModel("Some Game", covers, -5, null);

            Assert.Same(covers[1], tooHigh.Cover);
            Assert.Same(covers[0], tooLow.Cover);
        }

        [Fact]
        public void MoveNext_AdvancesToTheNextCover_AndStopsAtTheEnd()
        {
            var covers = new List<CoverDisplayItem> { MakeCover(Guid.NewGuid()), MakeCover(Guid.NewGuid()) };
            var viewModel = new CoverPreviewViewModel("Some Game", covers, 0, null);

            viewModel.MoveNext();

            Assert.Same(covers[1], viewModel.Cover);
            Assert.False(viewModel.CanMoveNext);

            viewModel.MoveNext(); // No-op past the end.

            Assert.Same(covers[1], viewModel.Cover);
        }

        [Fact]
        public void MovePrevious_RetreatsToThePreviousCover_AndStopsAtTheStart()
        {
            var covers = new List<CoverDisplayItem> { MakeCover(Guid.NewGuid()), MakeCover(Guid.NewGuid()) };
            var viewModel = new CoverPreviewViewModel("Some Game", covers, 1, null);

            viewModel.MovePrevious();

            Assert.Same(covers[0], viewModel.Cover);
            Assert.False(viewModel.CanMovePrevious);

            viewModel.MovePrevious(); // No-op past the start.

            Assert.Same(covers[0], viewModel.Cover);
        }

        [Fact]
        public void CanSetAsCurrent_RequiresACallback_AndThatTheCoverIsNotAlreadyCurrent()
        {
            var alreadyCurrent = MakeCover(Guid.NewGuid(), isCurrent: true);
            var withCallback = new CoverPreviewViewModel("Some Game", new List<CoverDisplayItem> { alreadyCurrent }, 0, id => { });
            var withoutCallback = new CoverPreviewViewModel("Some Game", new List<CoverDisplayItem> { MakeCover(Guid.NewGuid()) }, 0, null);

            Assert.False(withCallback.CanSetAsCurrent); // Callback present, but this cover is already current.
            Assert.False(withoutCallback.CanSetAsCurrent); // Not already current, but no callback supplied.
        }

        [Fact]
        public void SetAsCurrent_InvokesTheCallbackWithTheCurrentlyPreviewedCoverId()
        {
            var coverId = Guid.NewGuid();
            var covers = new List<CoverDisplayItem> { MakeCover(coverId) };
            Guid? invokedWith = null;
            var viewModel = new CoverPreviewViewModel("Some Game", covers, 0, id => invokedWith = id);

            viewModel.SetAsCurrent();

            Assert.Equal(coverId, invokedWith);
        }

        [Fact]
        public void SetAsCurrent_WhenCallbackDoesNotRefreshTheList_UpdatesIsCurrentLocally()
        {
            var first = MakeCover(Guid.NewGuid(), isCurrent: true);
            var second = MakeCover(Guid.NewGuid());
            var covers = new List<CoverDisplayItem> { first, second };
            var viewModel = new CoverPreviewViewModel("Some Game", covers, 1, id => { /* does not mutate the list */ });

            viewModel.SetAsCurrent();

            Assert.False(first.IsCurrent);
            Assert.True(second.IsCurrent);
            Assert.Equal("Set as current cover.", viewModel.StatusMessage);
        }

        [Fact]
        public void SetAsCurrent_WhenNotAllowed_DoesNothingAndDoesNotThrow()
        {
            var cover = MakeCover(Guid.NewGuid(), isCurrent: true);
            var viewModel = new CoverPreviewViewModel("Some Game", new List<CoverDisplayItem> { cover }, 0, id => throw new InvalidOperationException("Should not be called."));

            var exception = Record.Exception(() => viewModel.SetAsCurrent());

            Assert.Null(exception);
            Assert.Null(viewModel.StatusMessage);
        }

        [Fact]
        public void Constructor_WithEmptyCoverList_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CoverPreviewViewModel("Some Game", new List<CoverDisplayItem>(), 0, null));
        }
    }
}
