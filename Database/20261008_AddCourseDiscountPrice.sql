IF COL_LENGTH(N'erpsystem.tbltraining_courses', N'discount_price') IS NULL
BEGIN
    ALTER TABLE [erpsystem].[tbltraining_courses]
        ADD [discount_price] DECIMAL(18,2) NULL;
END;
