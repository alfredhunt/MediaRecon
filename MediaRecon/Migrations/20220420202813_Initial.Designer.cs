﻿// <auto-generated />
using System;
using ApexBytez.MediaRecon.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ApexBytez.MediaRecon.Migrations
{
    [DbContext(typeof(MediaReconContext))]
    [Migration("20220420202813_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.4");

            modelBuilder.Entity("ApexBytez.MediaRecon.DB.Directory", b =>
                {
                    b.Property<int>("DirectoryId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreationTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastAccessTime")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastWriteTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("DirectoryId");

                    b.ToTable("Directories");
                });

            modelBuilder.Entity("ApexBytez.MediaRecon.DB.File", b =>
                {
                    b.Property<int>("FileId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreationTime")
                        .HasColumnType("TEXT");

                    b.Property<int?>("DirectoryId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("DirectoryName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<byte[]>("Hash")
                        .IsRequired()
                        .HasColumnType("BLOB");

                    b.Property<string>("HashAlgorithm")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastAccessTime")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastWriteTime")
                        .HasColumnType("TEXT");

                    b.Property<long>("Length")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("FileId");

                    b.HasIndex("DirectoryId");

                    b.ToTable("Files");
                });

            modelBuilder.Entity("ApexBytez.MediaRecon.DB.File", b =>
                {
                    b.HasOne("ApexBytez.MediaRecon.DB.Directory", null)
                        .WithMany("Files")
                        .HasForeignKey("DirectoryId");
                });

            modelBuilder.Entity("ApexBytez.MediaRecon.DB.Directory", b =>
                {
                    b.Navigation("Files");
                });
#pragma warning restore 612, 618
        }
    }
}
