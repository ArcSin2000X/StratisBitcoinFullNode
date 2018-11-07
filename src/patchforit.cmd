set WorkingDirectory=%cd%
git apply %WorkingDirectory%\patches\AddDnsSeedAddress.patch
git apply %WorkingDirectory%\patches\AllowClientConnection_in_IBD.patch
git apply %WorkingDirectory%\patches\DecreaseDifficultyAndLastPOWBlockHeight.patch
git apply %WorkingDirectory%\patches\DecreaseCoinstakeMinConfirmation.patch